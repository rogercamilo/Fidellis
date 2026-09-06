using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

public sealed record LedgerLine(string Code, string Name, string Type, decimal Debit, decimal Credit, decimal Balance);

/// <summary>Demonstração do Resultado do Período (DRP): receitas − despesas = superávit/déficit.</summary>
public sealed record IncomeStatement(int Year, decimal Revenues, decimal Expenses, decimal Surplus,
    IReadOnlyList<LedgerLine> RevenueLines, IReadOnlyList<LedgerLine> ExpenseLines);

/// <summary>Balanço Patrimonial: Ativo = Passivo + PL (com o superávit do período no PL).</summary>
public sealed record BalanceSheet(int Year, decimal Assets, decimal Liabilities, decimal EquityAccounts,
    decimal Surplus, decimal TotalLiabilitiesAndEquity, bool Balanced,
    IReadOnlyList<LedgerLine> AssetLines, IReadOnlyList<LedgerLine> LiabilityLines, IReadOnlyList<LedgerLine> EquityLines);

/// <summary>
/// Demonstrações contábeis ITG 2002 (Onda 4 inc.4.0): balancete, DRP e Balanço Patrimonial, agregados
/// do razão (<c>accounting_entries</c> + <c>ledger_accounts</c>) do ano. Gera o <b>rascunho</b> — o
/// contador valida/assina fora (D2). Roda no schema do tenant.
/// </summary>
public sealed class StatementsService(TenantDbContext db)
{
    public async Task<IReadOnlyList<LedgerLine>> TrialBalanceAsync(int year, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);

        var entries = await db.AccountingEntries
            .Where(e => e.CreatedAt >= start && e.CreatedAt < end && e.LedgerAccountId != null)
            .Select(e => new { e.LedgerAccountId, e.Debit, e.Credit })
            .ToListAsync(ct);
        var accounts = await db.LedgerAccounts.ToDictionaryAsync(a => a.Id, a => a, ct);

        return entries
            .GroupBy(e => e.LedgerAccountId!.Value)
            .Where(g => accounts.ContainsKey(g.Key))
            .Select(g =>
            {
                var a = accounts[g.Key];
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                var balance = a.NormalBalance == "debit" ? debit - credit : credit - debit;
                return new LedgerLine(a.Code, a.Name, a.Type, debit, credit, balance);
            })
            .OrderBy(l => l.Code)
            .ToList();
    }

    public async Task<IncomeStatement> IncomeAsync(int year, CancellationToken ct = default)
    {
        var tb = await TrialBalanceAsync(year, ct);
        var revenueLines = tb.Where(l => l.Type == "revenue").ToList();
        var expenseLines = tb.Where(l => l.Type == "expense").ToList();
        var revenues = revenueLines.Sum(l => l.Balance);
        var expenses = expenseLines.Sum(l => l.Balance);
        return new IncomeStatement(year, revenues, expenses, revenues - expenses, revenueLines, expenseLines);
    }

    public async Task<BalanceSheet> BalanceSheetAsync(int year, CancellationToken ct = default)
    {
        var tb = await TrialBalanceAsync(year, ct);
        var assetLines = tb.Where(l => l.Type == "asset").ToList();
        var liabilityLines = tb.Where(l => l.Type == "liability").ToList();
        var equityLines = tb.Where(l => l.Type == "equity").ToList();

        var assets = assetLines.Sum(l => l.Balance);
        var liabilities = liabilityLines.Sum(l => l.Balance);
        var equityAccounts = equityLines.Sum(l => l.Balance);
        var surplus = tb.Where(l => l.Type == "revenue").Sum(l => l.Balance) - tb.Where(l => l.Type == "expense").Sum(l => l.Balance);
        var totalLE = liabilities + equityAccounts + surplus;

        return new BalanceSheet(year, assets, liabilities, equityAccounts, surplus, totalLE, assets == totalLE,
            assetLines, liabilityLines, equityLines);
    }
}
