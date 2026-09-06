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
    public async Task PostPaidAsync(Donation donation, CancellationToken ct = default)
    {
        var account = await EnsureAccountAsync(donation.OrganizationId, ct);

        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = donation.Amount,
            Kind = "credit",
            Description = $"Doação {donation.Id}",
            CostCenterId = donation.CostCenterId,
            ProjectId = donation.ProjectId,
            FundId = donation.FundId,
        };
        db.Transactions.Add(transaction);

        var (receivable, revenue) = await LedgerPairAsync(ct);
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = receivable.Id, Ledger = receivable.Name, Debit = donation.Amount, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = 0, Credit = donation.Amount });

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
    }

    /// <summary>
    /// Reverte uma doação conciliada (estorno/chargeback — RF-FIN-022): muda o status, lança a
    /// partida dobrada inversa e cancela o recibo. No-op se a doação não estava <c>paid</c>.
    /// </summary>
    public async Task ReverseAsync(Donation donation, string newStatus, string reason, CancellationToken ct = default)
    {
        if (donation.Status != "paid")
            return;
        donation.Status = newStatus;

        var account = await EnsureAccountAsync(donation.OrganizationId, ct);
        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = donation.Amount,
            Kind = "debit",
            Description = $"Estorno da doação {donation.Id} ({reason})",
            CostCenterId = donation.CostCenterId,
            ProjectId = donation.ProjectId,
            FundId = donation.FundId,
        };
        db.Transactions.Add(transaction);

        var (receivable, revenue) = await LedgerPairAsync(ct);
        // Inverso da conciliação: debita Receita, credita Recebível.
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = donation.Amount, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = receivable.Id, Ledger = receivable.Name, Debit = 0, Credit = donation.Amount });

        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.DonationId == donation.Id, ct);
        if (receipt is not null && receipt.CanceledAt is null)
        {
            receipt.CanceledAt = clock.UtcNow;
            receipt.CancelReason = reason;
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

    private async Task<(LedgerAccount Receivable, LedgerAccount Revenue)> LedgerPairAsync(CancellationToken ct)
    {
        await chartSeeder.EnsureDefaultAsync(ct);
        var accounts = await db.LedgerAccounts
            .Where(a => a.Code == ChartOfAccounts.Receivable || a.Code == ChartOfAccounts.Revenue)
            .ToDictionaryAsync(a => a.Code, a => a, ct);
        return (accounts[ChartOfAccounts.Receivable], accounts[ChartOfAccounts.Revenue]);
    }
}
