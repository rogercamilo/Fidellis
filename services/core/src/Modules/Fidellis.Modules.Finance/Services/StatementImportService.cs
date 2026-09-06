using Fidellis.Infrastructure.Banking;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Import de extrato bancário (Onda 3 inc.3.0): parseia o conteúdo (OFX nesta entrega) e persiste o
/// extrato + linhas, com dedupe por <c>fit_id</c> na conta (reimportação não duplica). Roda no schema
/// do tenant.
/// </summary>
public sealed class StatementImportService(TenantDbContext db)
{
    public async Task<(BankStatement Statement, int Imported, int Skipped)> ImportAsync(
        Guid accountId, string format, string? reference, string content, CancellationToken ct = default)
    {
        if (!await db.TreasuryAccounts.AnyAsync(a => a.Id == accountId, ct))
            throw new InvalidOperationException("Conta de tesouraria inexistente.");

        var fmt = (format ?? "ofx").Trim().ToLowerInvariant();
        if (fmt != "ofx")
            throw new InvalidOperationException("Formato não suportado nesta entrega (use 'ofx').");

        var parsed = OfxParser.Parse(content);

        // fit_ids já existentes nas linhas de extratos desta conta (dedupe entre importações).
        var accountStatementIds = db.BankStatements.Where(s => s.AccountId == accountId).Select(s => s.Id);
        var existingFitIds = (await db.BankStatementLines
                .Where(l => accountStatementIds.Contains(l.StatementId) && l.FitId != null)
                .Select(l => l.FitId!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var statement = new BankStatement { AccountId = accountId, Format = fmt, Reference = reference };
        db.BankStatements.Add(statement);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int imported = 0, skipped = 0;
        foreach (var tx in parsed)
        {
            if (tx.FitId is { Length: > 0 } fit && (existingFitIds.Contains(fit) || !seen.Add(fit)))
            {
                skipped++;
                continue;
            }
            db.BankStatementLines.Add(new BankStatementLine
            {
                StatementId = statement.Id,
                FitId = tx.FitId,
                PostedAt = tx.PostedAt,
                Amount = tx.Amount,
                Memo = tx.Memo,
            });
            imported++;
        }

        await db.SaveChangesAsync(ct);
        return (statement, imported, skipped);
    }
}
