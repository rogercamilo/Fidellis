using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Candidato de casamento sugerido para uma linha de extrato. <paramref name="Kind"/>: <c>exact</c>
/// (valor bate com o saldo remanescente da linha) ou <c>partial</c> (quita/consome só em parte).
/// </summary>
public sealed record MatchCandidate(string Type, Guid Id, string Description, decimal Amount, DateOnly Date, string Kind);

/// <summary>
/// Casamento de conciliação (Onda 3 inc.3.1; DT-08): sugere candidatos por <b>data (± janela)</b> —
/// exatos e <b>parciais</b> — e, ao confirmar, aplica a baixa consumindo (parcial ou totalmente) o
/// saldo da linha. Suporta <b>N:1</b> (uma linha quita vários títulos, chamando <c>Match</c> em
/// sequência) e <b>1:N</b> (vários lançamentos quitam um título, via baixa acumulada). Recebível
/// (entrada) via <see cref="ReceivablesService"/> + entrada de tesouraria; pagável (saída) via
/// <see cref="PayablesService"/> (pagamento integral). Roda no schema do tenant.
/// </summary>
public sealed class ReconciliationMatchService(
    TenantDbContext db,
    ReceivablesService receivables,
    PayablesService payables)
{
    /// <summary>Janela de data (dias) para o casamento — decisão D3.</summary>
    public const int MatchWindowDays = 3;

    /// <summary>Saldo ainda não casado da linha (em módulo).</summary>
    private static decimal Remaining(BankStatementLine line) => Math.Abs(line.Amount) - line.MatchedAmount;

    public async Task<IReadOnlyList<MatchCandidate>> SuggestAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null || line.Status is "matched" or "ignored") return [];
        var remaining = Remaining(line);
        if (remaining <= 0) return [];

        if (line.Amount > 0)
        {
            var open = await db.Receivables
                .Where(r => r.Status == "open" || r.Status == "partial")
                .Select(r => new { r.Id, r.Description, r.Source, r.Amount, r.ReceivedAmount, r.DueDate })
                .ToListAsync(ct);
            return open
                .Select(r => new { r.Id, r.Description, r.Source, Outstanding = r.Amount - r.ReceivedAmount, r.DueDate })
                .Where(r => r.Outstanding > 0 && WithinWindow(r.DueDate, line.PostedAt))
                // Exatos primeiro; depois os mais próximos do saldo da linha.
                .OrderByDescending(r => r.Outstanding == remaining)
                .ThenBy(r => Math.Abs(r.Outstanding - remaining))
                .Select(r => new MatchCandidate("receivable", r.Id, r.Description ?? r.Source, r.Outstanding, r.DueDate,
                    r.Outstanding == remaining ? "exact" : "partial"))
                .ToList();
        }

        var approved = await db.Payables
            .Where(p => p.Status == "approved")
            .Select(p => new { p.Id, p.Description, p.Amount, p.DueDate })
            .ToListAsync(ct);
        // Pagável é quitado integralmente: só casa com o saldo remanescente exato da linha.
        return approved
            .Where(p => p.Amount == remaining && WithinWindow(p.DueDate, line.PostedAt))
            .Select(p => new MatchCandidate("payable", p.Id, p.Description, p.Amount, p.DueDate, "exact"))
            .ToList();
    }

    /// <summary>
    /// Confirma o casamento de uma linha com um recebível/pagável e aplica a baixa. Se
    /// <paramref name="amount"/> for informado (baixa parcial), aplica esse valor; senão consome o
    /// saldo remanescente da linha (limitado ao saldo do título, no caso de recebível). A linha vira
    /// <c>partial</c> enquanto sobrar saldo e <c>matched</c> quando atinge o total.
    /// </summary>
    public async Task<BankStatementLine> MatchAsync(Guid lineId, string type, Guid id, decimal? amount = null, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct)
            ?? throw new InvalidOperationException("Linha de extrato não encontrada.");
        if (line.Status is "matched" or "ignored")
            throw new InvalidOperationException("Linha já conciliada ou ignorada.");
        if (amount is { } a && a <= 0)
            throw new InvalidOperationException("O valor da baixa deve ser positivo.");

        var remaining = Remaining(line);
        if (remaining <= 0) throw new InvalidOperationException("Linha sem saldo a casar.");

        var accountId = await db.BankStatements.Where(s => s.Id == line.StatementId).Select(s => s.AccountId).FirstAsync(ct);
        decimal applied;

        if (type == "receivable")
        {
            if (line.Amount <= 0) throw new InvalidOperationException("Recebível casa com uma entrada (valor positivo).");
            var receivable = await db.Receivables.FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new InvalidOperationException("Recebível não encontrado.");
            var outstanding = receivable.Amount - receivable.ReceivedAmount;
            if (outstanding <= 0) throw new InvalidOperationException("Recebível já quitado.");

            // Consome no máximo o saldo da linha e o saldo do título (permite parcial, N:1 e 1:N).
            applied = Math.Min(amount ?? remaining, Math.Min(remaining, outstanding));
            await receivables.SettleAsync(receivable.Id, applied, null, ct);
            db.TreasuryMovements.Add(new TreasuryMovement
            {
                AccountId = accountId, Kind = "inflow", Amount = applied,
                Description = $"Recebimento (extrato) {line.Memo}".Trim(),
            });
            line.MatchedType ??= "receivable";
            line.MatchedId ??= receivable.Id;
        }
        else if (type == "payable")
        {
            if (line.Amount >= 0) throw new InvalidOperationException("Pagável casa com uma saída (valor negativo).");
            var payable = await db.Payables.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new InvalidOperationException("Título a pagar não encontrado.");
            if (payable.Amount > remaining)
                throw new InvalidOperationException("O título a pagar é maior que o saldo da linha (pagamento integral).");
            await payables.PayAsync(id, accountId, ct);
            applied = payable.Amount;
            line.MatchedType ??= "payable";
            line.MatchedId ??= payable.Id;
        }
        else
        {
            throw new InvalidOperationException("type deve ser 'receivable' ou 'payable'.");
        }

        line.MatchedAmount += applied;
        line.Status = Remaining(line) <= 0.005m ? "matched" : "partial";
        await db.SaveChangesAsync(ct);
        return line;
    }

    public async Task<BankStatementLine?> IgnoreAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) return null;
        if (line.Status is "matched" or "partial") throw new InvalidOperationException("Linha já conciliada não pode ser ignorada.");
        line.Status = "ignored";
        await db.SaveChangesAsync(ct);
        return line;
    }

    private static bool WithinWindow(DateOnly a, DateOnly b)
        => Math.Abs(a.DayNumber - b.DayNumber) <= MatchWindowDays;
}
