using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>Candidato de casamento sugerido para uma linha de extrato.</summary>
public sealed record MatchCandidate(string Type, Guid Id, string Description, decimal Amount, DateOnly Date);

/// <summary>
/// Casamento de conciliação (Onda 3 inc.3.1): sugere candidatos para cada linha do extrato por
/// <b>valor + data (± janela)</b> e, ao confirmar, aplica a baixa — recebível (entrada) via
/// <see cref="ReceivablesService"/> + entrada de tesouraria; pagável (saída) via
/// <see cref="PayablesService"/> (pagamento). Reusa os serviços existentes. Roda no schema do tenant.
/// </summary>
public sealed class ReconciliationMatchService(
    TenantDbContext db,
    ReceivablesService receivables,
    PayablesService payables)
{
    /// <summary>Janela de data (dias) para o casamento — decisão D3.</summary>
    public const int MatchWindowDays = 3;

    public async Task<IReadOnlyList<MatchCandidate>> SuggestAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null || line.Status != "unmatched") return [];

        if (line.Amount > 0)
        {
            var open = await db.Receivables
                .Where(r => r.Status == "open" || r.Status == "partial")
                .Select(r => new { r.Id, r.Description, r.Source, r.Amount, r.ReceivedAmount, r.DueDate })
                .ToListAsync(ct);
            return open
                .Where(r => r.Amount - r.ReceivedAmount == line.Amount && WithinWindow(r.DueDate, line.PostedAt))
                .Select(r => new MatchCandidate("receivable", r.Id, r.Description ?? r.Source, r.Amount - r.ReceivedAmount, r.DueDate))
                .ToList();
        }

        var amount = -line.Amount;
        var approved = await db.Payables
            .Where(p => p.Status == "approved")
            .Select(p => new { p.Id, p.Description, p.Amount, p.DueDate })
            .ToListAsync(ct);
        return approved
            .Where(p => p.Amount == amount && WithinWindow(p.DueDate, line.PostedAt))
            .Select(p => new MatchCandidate("payable", p.Id, p.Description, p.Amount, p.DueDate))
            .ToList();
    }

    /// <summary>Confirma o casamento de uma linha com um recebível/pagável e aplica a baixa.</summary>
    public async Task<BankStatementLine> MatchAsync(Guid lineId, string type, Guid id, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct)
            ?? throw new InvalidOperationException("Linha de extrato não encontrada.");
        if (line.Status != "unmatched")
            throw new InvalidOperationException("Linha já conciliada ou ignorada.");

        var accountId = await db.BankStatements.Where(s => s.Id == line.StatementId).Select(s => s.AccountId).FirstAsync(ct);

        if (type == "receivable")
        {
            if (line.Amount <= 0) throw new InvalidOperationException("Recebível casa com uma entrada (valor positivo).");
            var settled = await receivables.SettleAsync(id, line.Amount, null, ct)
                ?? throw new InvalidOperationException("Recebível não encontrado.");
            db.TreasuryMovements.Add(new TreasuryMovement
            {
                AccountId = accountId, Kind = "inflow", Amount = line.Amount,
                Description = $"Recebimento (extrato) {line.Memo}".Trim(),
            });
            line.MatchedType = "receivable";
            line.MatchedId = settled.Id;
        }
        else if (type == "payable")
        {
            if (line.Amount >= 0) throw new InvalidOperationException("Pagável casa com uma saída (valor negativo).");
            var paid = await payables.PayAsync(id, accountId, ct)
                ?? throw new InvalidOperationException("Título a pagar não encontrado.");
            line.MatchedType = "payable";
            line.MatchedId = paid.Id;
        }
        else
        {
            throw new InvalidOperationException("type deve ser 'receivable' ou 'payable'.");
        }

        line.Status = "matched";
        await db.SaveChangesAsync(ct);
        return line;
    }

    public async Task<BankStatementLine?> IgnoreAsync(Guid lineId, CancellationToken ct = default)
    {
        var line = await db.BankStatementLines.FirstOrDefaultAsync(l => l.Id == lineId, ct);
        if (line is null) return null;
        if (line.Status == "matched") throw new InvalidOperationException("Linha já conciliada não pode ser ignorada.");
        line.Status = "ignored";
        await db.SaveChangesAsync(ct);
        return line;
    }

    private static bool WithinWindow(DateOnly a, DateOnly b)
        => Math.Abs(a.DayNumber - b.DayNumber) <= MatchWindowDays;
}
