using System.Globalization;
using System.Text;
using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Exportação para o contador (Onda 4 inc.4.3 / RF-FIN-165 / decisão D2): razão e balancete do ano em
/// CSV (separador <c>;</c>), pronto para importar no sistema contábil. SPED ECD fica como evolução.
/// Roda no schema do tenant.
/// </summary>
public sealed class AccountantExportService(TenantDbContext db, StatementsService statements)
{
    public async Task<string> LedgerCsvAsync(int year, CancellationToken ct = default)
    {
        // Competência (DT-07): recorte e ordenação por data contábil do lançamento.
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year + 1, 1, 1);

        var rows = await (
            from e in db.AccountingEntries
            join t in db.Transactions on e.TransactionId equals t.Id
            where e.AccountingDate >= start && e.AccountingDate < end
            orderby e.AccountingDate
            select new { e.AccountingDate, e.LedgerAccountId, e.Debit, e.Credit, t.Description })
            .ToListAsync(ct);
        var accounts = await db.LedgerAccounts.ToDictionaryAsync(a => a.Id, a => a, ct);

        var sb = new StringBuilder();
        sb.AppendLine("data;codigo;conta;debito;credito;historico");
        foreach (var r in rows)
        {
            var acc = r.LedgerAccountId is { } id && accounts.TryGetValue(id, out var a) ? a : null;
            sb.Append(r.AccountingDate.ToString("yyyy-MM-dd")).Append(';')
              .Append(acc?.Code ?? "").Append(';')
              .Append(Csv(acc?.Name ?? "")).Append(';')
              .Append(Money(r.Debit)).Append(';')
              .Append(Money(r.Credit)).Append(';')
              .Append(Csv(r.Description ?? "")).Append('\n');
        }
        return sb.ToString();
    }

    public async Task<string> TrialBalanceCsvAsync(int year, CancellationToken ct = default)
    {
        var lines = await statements.TrialBalanceAsync(year, ct);
        var sb = new StringBuilder();
        sb.AppendLine("codigo;conta;tipo;debito;credito;saldo");
        foreach (var l in lines)
            sb.Append(l.Code).Append(';').Append(Csv(l.Name)).Append(';').Append(l.Type).Append(';')
              .Append(Money(l.Debit)).Append(';').Append(Money(l.Credit)).Append(';').Append(Money(l.Balance)).Append('\n');
        return sb.ToString();
    }

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Csv(string s) => s.Contains(';') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
