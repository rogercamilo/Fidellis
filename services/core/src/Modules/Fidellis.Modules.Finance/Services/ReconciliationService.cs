using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Conciliação contábil de uma doação: partida dobrada contra o plano de contas + recibo + régua, e
/// a <b>reversão</b> em estorno/chargeback. Reusada pelo <see cref="WebhookProcessor"/> (PIX/boleto) e
/// pelo cartão síncrono (<see cref="DonationCheckoutService"/>). Não faz <c>SaveChanges</c> — quem
/// chama persiste.
/// </summary>
public sealed class ReconciliationService(
    TenantDbContext db,
    ChartOfAccountsSeeder chartSeeder,
    ReceiptService receipts,
    INotifier notifier,
    IClock clock)
{
    /// <summary>
    /// Concilia uma doação já marcada como <c>paid</c> (com <c>PaidAt</c>): cria a transação, os
    /// lançamentos de partida dobrada, emite o recibo, enfileira o agradecimento e — se for ciclo
    /// recorrente — zera o dunning e reagenda a próxima cobrança.
    /// </summary>
    public async Task PostPaidAsync(Entry donation, CancellationToken ct = default)
    {
        var account = await EnsureAccountAsync(donation.OrganizationId, ct);

        // Competência (DT-07): data do pagamento da doação.
        var date = DateOnly.FromDateTime((donation.PaidAt ?? clock.UtcNow).UtcDateTime);
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = donation.Amount,
            Kind = "credit",
            Description = $"Doação {donation.Id}",
            CostCenterId = donation.CostCenterId,
            ProjectId = donation.ProjectId,
            FundId = donation.FundId,
            AccountingDate = date,
        };
        db.Transactions.Add(transaction);

        var (cash, revenue) = await LedgerPairAsync(ct);
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = cash.Id, Ledger = cash.Name, Debit = donation.Amount, Credit = 0, AccountingDate = date },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = 0, Credit = donation.Amount, AccountingDate = date });

        await AddTreasuryAsync(donation, "inflow", ct);

        var donorDocument = donation.DonorId is { } donorId
            ? await db.Donors.Where(d => d.Id == donorId).Select(d => d.Document).FirstOrDefaultAsync(ct)
            : null;
        var receipt = await receipts.GenerateForDonationAsync(donation, donation.DonorName ?? "Doador", donorDocument, ct);

        await notifier.DonationPaidAsync(donation, receipt.Number, ct);

        if (donation.RecurringDonationId is { } recurringId)
        {
            var recurring = await db.RecurringDonations.FirstOrDefaultAsync(r => r.Id == recurringId, ct);
            if (recurring is not null && recurring.Status is "active" or "past_due")
            {
                recurring.Attempt = 0;
                recurring.Status = "active";
                recurring.NextChargeAt = RecurringBillingService.NextChargeDate(recurring.DayOfMonth, clock.UtcNow.AddDays(1));
            }
        }

        // Baixa do título a receber vinculado (Onda 2 — RF-FIN-103, vínculo explícito).
        if (donation.ReceivableId is { } receivableId)
        {
            var linkedReceivable = await db.Receivables.FirstOrDefaultAsync(r => r.Id == receivableId, ct);
            if (linkedReceivable is not null)
                ReceivablesService.Apply(linkedReceivable, donation.Amount, donation.Id);
        }
    }

    /// <summary>
    /// Reverte uma doação conciliada (estorno/chargeback — RF-FIN-022): muda o status, lança a
    /// partida dobrada inversa e cancela o recibo. No-op se a doação não estava <c>paid</c>.
    /// </summary>
    public async Task ReverseAsync(Entry donation, string newStatus, string reason, CancellationToken ct = default)
    {
        if (donation.Status != "paid")
            return;
        donation.Status = newStatus;

        var account = await EnsureAccountAsync(donation.OrganizationId, ct);
        // Competência (DT-07): o estorno é fato do momento em que ocorre.
        var date = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = donation.Amount,
            Kind = "debit",
            Description = $"Estorno da doação {donation.Id} ({reason})",
            CostCenterId = donation.CostCenterId,
            ProjectId = donation.ProjectId,
            FundId = donation.FundId,
            AccountingDate = date,
        };
        db.Transactions.Add(transaction);

        var (cash, revenue) = await LedgerPairAsync(ct);
        // Inverso da conciliação: debita Receita, credita Banco.
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = donation.Amount, Credit = 0, AccountingDate = date },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = cash.Id, Ledger = cash.Name, Debit = 0, Credit = donation.Amount, AccountingDate = date });

        await AddTreasuryAsync(donation, "outflow", ct);

        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.DonationId == donation.Id, ct);
        if (receipt is not null && receipt.CanceledAt is null)
        {
            receipt.CanceledAt = clock.UtcNow;
            receipt.CancelReason = reason;
        }
    }

    /// <summary>
    /// Contabiliza uma entrada de 1ª classe já paga vinda de <b>caixa físico</b> ou <b>lançamento
    /// manual</b> (D-05): partida dobrada (débito na conta de ativo <paramref name="assetLedgerCode"/> —
    /// Caixa ou Banco — / crédito Receita), movimento de tesouraria na conta informada e recibo +
    /// agradecimento <b>apenas quando há doador identificado</b> (recibo condicional). Reusa o mesmo
    /// motor da conciliação do PSP, então a entrada aparece igual em relatórios, transparência e CRM.
    /// Não faz <c>SaveChanges</c> — quem chama persiste.
    /// </summary>
    public async Task PostEntryAsync(
        Entry entry, TreasuryAccount treasuryAccount, string assetLedgerCode, CancellationToken ct = default)
    {
        var account = await EnsureAccountAsync(entry.OrganizationId, ct);
        var date = DateOnly.FromDateTime((entry.PaidAt ?? clock.UtcNow).UtcDateTime);
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = entry.Amount,
            Kind = "credit",
            Description = $"Entrada {entry.Id}",
            CostCenterId = entry.CostCenterId,
            ProjectId = entry.ProjectId,
            FundId = entry.FundId,
            AccountingDate = date,
        };
        db.Transactions.Add(transaction);

        var (asset, revenue) = await LedgerPairAsync(assetLedgerCode, ct);
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = asset.Id, Ledger = asset.Name, Debit = entry.Amount, Credit = 0, AccountingDate = date },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = 0, Credit = entry.Amount, AccountingDate = date });

        db.TreasuryMovements.Add(new TreasuryMovement
        {
            AccountId = treasuryAccount.Id,
            Kind = "inflow",
            Amount = entry.Amount,
            Description = $"Entrada {entry.Id}",
            DonationId = entry.Id,
            OccurredAt = entry.PaidAt ?? clock.UtcNow,
        });

        // Recibo condicional (D-05 Q3): só quando há doador identificado (caixa anônimo não emite).
        if (entry.DonorId is { } donorId)
        {
            var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == donorId, ct);
            if (donor is not null)
            {
                var receipt = await receipts.GenerateForDonationAsync(entry, donor.Name, donor.Document, ct);
                await notifier.DonationPaidAsync(entry, receipt.Number, ct);
            }
        }
    }

    private async Task<Account> EnsureAccountAsync(Guid organizationId, CancellationToken ct)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.OrganizationId == organizationId, ct);
        if (account is null)
        {
            account = new Account { OrganizationId = organizationId, Name = "Conta principal" };
            db.Accounts.Add(account);
        }
        return account;
    }

    // DT-02: a doação recebida entra no Caixa/Banco (ativo), não em "a receber" — assim o Balanço fecha
    // (Ativo Banco = Receita − Despesa = superávit). A doação via PSP debita o Banco (default).
    private Task<(LedgerAccount Asset, LedgerAccount Revenue)> LedgerPairAsync(CancellationToken ct)
        => LedgerPairAsync(ChartOfAccounts.Bank, ct);

    /// <summary>Par (ativo, receita) do razão para a partida dobrada — o ativo varia por origem (D-05).</summary>
    private async Task<(LedgerAccount Asset, LedgerAccount Revenue)> LedgerPairAsync(string assetCode, CancellationToken ct)
    {
        await chartSeeder.EnsureDefaultAsync(ct);
        var accounts = await db.LedgerAccounts
            .Where(a => a.Code == assetCode || a.Code == ChartOfAccounts.Revenue)
            .ToDictionaryAsync(a => a.Code, a => a, ct);
        return (accounts[assetCode], accounts[ChartOfAccounts.Revenue]);
    }

    /// <summary>Movimento de tesouraria na conta bancária da unidade (integra razão ↔ tesouraria — DT-02).</summary>
    private async Task AddTreasuryAsync(Entry donation, string kind, CancellationToken ct)
    {
        var bank = await db.TreasuryAccounts
            .FirstOrDefaultAsync(a => a.OrganizationId == donation.OrganizationId && a.Kind == "bank" && a.Active, ct);
        if (bank is null) return;
        db.TreasuryMovements.Add(new TreasuryMovement
        {
            AccountId = bank.Id,
            Kind = kind,
            Amount = donation.Amount,
            Description = $"Doação {donation.Id}",
            DonationId = donation.Id,
            OccurredAt = donation.PaidAt ?? clock.UtcNow,
        });
    }
}
