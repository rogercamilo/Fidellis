using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Caixa físico (RF-FIN-132): abre uma sessão num caixa (conta de tesouraria <c>cash</c>), fecha com o
/// valor conferido e a <b>dupla conferência</b> (2º responsável ≠ de quem abriu — decisão D6), e
/// deposita (transferência do caixa para a conta bancária). No fechamento, a coleta em espécie vira uma
/// <b>entrada de 1ª classe</b> (receita + dimensão + tesouraria — D-05), não só um saldo de caixa.
/// Roda no schema do tenant.
/// </summary>
public sealed class CashSessionService(TenantDbContext db, TreasuryService treasury, ReconciliationService reconciliation, IClock clock)
{
    public async Task<CashSession> OpenAsync(Guid accountId, Guid openedBy, string? eventLabel, CancellationToken ct = default)
    {
        var account = await db.TreasuryAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new InvalidOperationException("Conta de tesouraria inexistente.");
        if (account.Kind != "cash")
            throw new InvalidOperationException("Sessão de caixa só abre em conta do tipo 'cash'.");
        if (await db.CashSessions.AnyAsync(s => s.AccountId == accountId && s.Status == "open", ct))
            throw new InvalidOperationException("Já existe uma sessão aberta neste caixa.");

        var session = new CashSession { AccountId = accountId, OpenedBy = openedBy, OpenedAt = clock.UtcNow, EventLabel = eventLabel };
        db.CashSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>Fecha a sessão com o valor conferido; exige um segundo responsável (dupla conferência).</summary>
    public async Task<CashSession> CloseAsync(Guid sessionId, decimal countedAmount, Guid confirmedBy, CancellationToken ct = default)
    {
        var session = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Sessão não encontrada.");
        if (session.Status != "open")
            throw new InvalidOperationException("Sessão já fechada.");
        if (countedAmount < 0)
            throw new InvalidOperationException("O valor conferido não pode ser negativo.");
        if (confirmedBy == session.OpenedBy)
            throw new InvalidOperationException("A dupla conferência exige um segundo responsável (diferente de quem abriu).");

        session.CountedAmount = countedAmount;
        session.ConfirmedBy = confirmedBy;
        session.ClosedAt = clock.UtcNow;
        session.Status = "closed";

        // D-05: a coleta em espécie vira uma ENTRADA de 1ª classe (receita + dimensão + tesouraria),
        // não só um saldo de caixa. Agregada pelo total conferido (Q2); débito no Caixa, crédito Receita;
        // sem recibo (coleta anônima — Q3). O depósito posterior segue como transferência caixa→banco.
        if (countedAmount > 0)
        {
            var account = await db.TreasuryAccounts.FirstAsync(a => a.Id == session.AccountId, ct);
            var entry = new Donation
            {
                OrganizationId = account.OrganizationId,
                Amount = countedAmount,
                Method = "cash",
                Source = "cash",
                Status = "paid",
                PaidAt = clock.UtcNow,
                DonorName = string.IsNullOrWhiteSpace(session.EventLabel) ? "Coleta em espécie" : $"Coleta — {session.EventLabel}",
                CostCenterId = await db.CostCenters.Where(c => c.IsDefault).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct),
                FundId = await db.Funds.Where(f => f.IsDefault).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct),
            };
            db.Donations.Add(entry);
            await reconciliation.PostEntryAsync(entry, account, ChartOfAccounts.Cash, ct);
        }

        await db.SaveChangesAsync(ct);
        return session;
    }

    /// <summary>Deposita o valor da sessão fechada na conta bancária (transferência caixa→banco).</summary>
    public async Task<CashSession> DepositAsync(Guid sessionId, Guid bankAccountId, CancellationToken ct = default)
    {
        var session = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Sessão não encontrada.");
        if (session.Status != "closed")
            throw new InvalidOperationException("Só sessão fechada pode ser depositada.");
        if (session.DepositedMovementId is not null)
            throw new InvalidOperationException("Sessão já depositada.");
        if (session.CountedAmount is not { } amount || amount <= 0)
            throw new InvalidOperationException("Sessão sem valor a depositar.");

        var (outflow, _) = await treasury.TransferAsync(session.AccountId, bankAccountId, amount, "Depósito de caixa", ct);
        session.DepositedMovementId = outflow.Id;
        await db.SaveChangesAsync(ct);
        return session;
    }
}
