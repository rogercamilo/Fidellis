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

    /// <summary>
    /// Fecha a sessão com o valor conferido; exige um segundo responsável (dupla conferência). A coleta
    /// pode ser <b>discriminada</b> em linhas por tipo/finalidade (D-06 — Σ das linhas == conferido); sem
    /// linhas, gera uma entrada agregada de <c>oferta</c>.
    /// </summary>
    public async Task<CashSession> CloseAsync(
        Guid sessionId, decimal countedAmount, Guid confirmedBy, IReadOnlyList<CashEntryLine>? lines = null, CancellationToken ct = default)
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

        // D-05/D-06: a coleta em espécie vira ENTRADA(s) de 1ª classe (receita + dimensão + tesouraria),
        // não só um saldo de caixa. Débito no Caixa, crédito Receita; sem recibo (coleta anônima). Pode
        // ser discriminada por tipo (D-06); sem discriminação, uma entrada agregada de oferta. O depósito
        // posterior segue como transferência caixa→banco.
        if (countedAmount > 0)
        {
            var account = await db.TreasuryAccounts.FirstAsync(a => a.Id == session.AccountId, ct);
            var defaultCc = await db.CostCenters.Where(c => c.IsDefault).Select(c => (Guid?)c.Id).FirstOrDefaultAsync(ct);
            var defaultFund = await db.Funds.Where(f => f.IsDefault).Select(f => (Guid?)f.Id).FirstOrDefaultAsync(ct);

            IReadOnlyList<CashEntryLine> entryLines;
            if (lines is { Count: > 0 })
            {
                if (lines.Any(l => l.Amount <= 0))
                    throw new InvalidOperationException("Cada linha da coleta deve ter valor positivo.");
                if (lines.Any(l => !EntryTypes.IsValid(l.EntryType)))
                    throw new InvalidOperationException("Tipo de entrada inválido numa das linhas da coleta.");
                if (lines.Sum(l => l.Amount) != countedAmount)
                    throw new InvalidOperationException("A soma das linhas deve ser igual ao valor conferido.");
                entryLines = lines;
            }
            else
            {
                entryLines = [new CashEntryLine(EntryTypes.Offering, countedAmount)];
            }

            var label = string.IsNullOrWhiteSpace(session.EventLabel) ? "Coleta em espécie" : $"Coleta — {session.EventLabel}";
            foreach (var line in entryLines)
            {
                var entry = new Donation
                {
                    OrganizationId = account.OrganizationId,
                    Amount = line.Amount,
                    Method = "cash",
                    Source = "cash",
                    EntryType = line.EntryType,
                    Status = "paid",
                    PaidAt = clock.UtcNow,
                    DonorName = label,
                    CostCenterId = line.CostCenterId ?? defaultCc,
                    ProjectId = line.ProjectId,
                    FundId = line.FundId ?? defaultFund,
                };
                db.Donations.Add(entry);
                await reconciliation.PostEntryAsync(entry, account, ChartOfAccounts.Cash, ct);
            }
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

/// <summary>Linha discriminada da coleta de caixa (D-06): tipo + valor + finalidade (dimensões).</summary>
public sealed record CashEntryLine(
    string EntryType, decimal Amount, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);
