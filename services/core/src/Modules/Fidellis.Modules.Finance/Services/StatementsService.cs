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

/// <summary>Bloco de resultado (receitas/despesas/superávit) de um recorte de restrição.</summary>
public sealed record ResultBlock(decimal Revenues, decimal Expenses, decimal Surplus);

/// <summary>DRP segregada por recurso livre × restrito (ITG 2002).</summary>
public sealed record SegregatedIncome(int Year, ResultBlock Free, ResultBlock Restricted, ResultBlock Total);

/// <summary>Demonstração das Mutações do Patrimônio Líquido.</summary>
public sealed record Dmpl(int Year, decimal OpeningEquity, decimal Surplus, decimal ClosingEquity,
    decimal FreeSurplus, decimal RestrictedSurplus);

/// <summary>Demonstração dos Fluxos de Caixa (método direto): entradas − saídas de tesouraria.</summary>
public sealed record CashFlowStatement(int Year, decimal OpeningCash, decimal Inflows, decimal Outflows,
    decimal NetCash, decimal ClosingCash);

/// <summary>Resumo público de transparência (consolidado trimestral): resultado do período + balanço resumido.</summary>
public sealed record TransparencySummary(int Year, int? Quarter, decimal Revenues, decimal Expenses,
    decimal Surplus, decimal Assets, decimal Liabilities, decimal NetEquity);

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

    /// <summary>DRP segregada por recurso livre × restrito (RF-FIN-161).</summary>
    public async Task<SegregatedIncome> IncomeSegregatedAsync(int year, CancellationToken ct = default)
    {
        var agg = await AggregateByTypeRestrictionAsync(year, ct);
        ResultBlock Block(string r)
        {
            var rev = agg.GetValueOrDefault(("revenue", r), 0m);
            var exp = agg.GetValueOrDefault(("expense", r), 0m);
            return new ResultBlock(rev, exp, rev - exp);
        }
        var free = Block("free");
        var restricted = Block("restricted");
        var total = new ResultBlock(free.Revenues + restricted.Revenues, free.Expenses + restricted.Expenses, free.Surplus + restricted.Surplus);
        return new SegregatedIncome(year, free, restricted, total);
    }

    /// <summary>DMPL: PL inicial + superávit/déficit do período = PL final (RF-FIN-160).</summary>
    public async Task<Dmpl> DmplAsync(int year, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // PL inicial = saldo das contas de PL a partir dos lançamentos anteriores ao ano.
        var prior = await db.AccountingEntries
            .Where(e => e.CreatedAt < start && e.LedgerAccountId != null)
            .Select(e => new { e.LedgerAccountId, e.Debit, e.Credit })
            .ToListAsync(ct);
        var accounts = await db.LedgerAccounts.ToDictionaryAsync(a => a.Id, a => a, ct);
        var opening = prior
            .Where(e => accounts.TryGetValue(e.LedgerAccountId!.Value, out var a) && a.Type == "equity")
            .Sum(e => accounts[e.LedgerAccountId!.Value].NormalBalance == "debit" ? e.Debit - e.Credit : e.Credit - e.Debit);

        var seg = await IncomeSegregatedAsync(year, ct);
        return new Dmpl(year, opening, seg.Total.Surplus, opening + seg.Total.Surplus, seg.Free.Surplus, seg.Restricted.Surplus);
    }

    /// <summary>
    /// Resumo público de transparência (RF-FIN-164 / decisão 4): resultado do período (ano ou
    /// trimestre) + balanço resumido consolidado no fim do período. Sem dados pessoais.
    /// </summary>
    public async Task<TransparencySummary> TransparencyAsync(int year, int? quarter, CancellationToken ct = default)
    {
        var (start, end) = QuarterBounds(year, quarter);
        var period = await LinesAsync(start, end, ct);
        var revenues = period.Where(l => l.Type == "revenue").Sum(l => l.Balance);
        var expenses = period.Where(l => l.Type == "expense").Sum(l => l.Balance);

        var snapshot = await LinesAsync(null, end, ct);
        var assets = snapshot.Where(l => l.Type == "asset").Sum(l => l.Balance);
        var liabilities = snapshot.Where(l => l.Type == "liability").Sum(l => l.Balance);
        var equityAccounts = snapshot.Where(l => l.Type == "equity").Sum(l => l.Balance);
        var accumulatedSurplus = snapshot.Where(l => l.Type == "revenue").Sum(l => l.Balance)
                                 - snapshot.Where(l => l.Type == "expense").Sum(l => l.Balance);

        return new TransparencySummary(year, quarter, revenues, expenses, revenues - expenses,
            assets, liabilities, equityAccounts + accumulatedSurplus);
    }

    private static (DateTimeOffset Start, DateTimeOffset End) QuarterBounds(int year, int? quarter)
    {
        if (quarter is >= 1 and <= 4)
        {
            var s = new DateTimeOffset(year, (quarter.Value - 1) * 3 + 1, 1, 0, 0, 0, TimeSpan.Zero);
            return (s, s.AddMonths(3));
        }
        var ys = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return (ys, ys.AddYears(1));
    }

    private async Task<List<LedgerLine>> LinesAsync(DateTimeOffset? from, DateTimeOffset to, CancellationToken ct)
    {
        var q = db.AccountingEntries.Where(e => e.CreatedAt < to && e.LedgerAccountId != null);
        if (from is { } f) q = q.Where(e => e.CreatedAt >= f);
        var entries = await q.Select(e => new { e.LedgerAccountId, e.Debit, e.Credit }).ToListAsync(ct);
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

    /// <summary>DFC método direto (RF-FIN-160 / decisão D1): entradas − saídas de tesouraria no ano.</summary>
    public async Task<CashFlowStatement> CashFlowAsync(int year, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);

        var openingBalances = await db.TreasuryAccounts.SumAsync(a => a.OpeningBalance, ct);
        var movements = await db.TreasuryMovements.Select(m => new { m.Kind, m.Amount, m.OccurredAt }).ToListAsync(ct);

        // Delta consolidado: entradas somam, saídas subtraem; transferências internas se cancelam no total.
        static decimal Delta(IEnumerable<(string Kind, decimal Amount)> ms)
            => ms.Sum(m => m.Kind is "inflow" or "transfer_in" ? m.Amount : -m.Amount);

        var opening = openingBalances + Delta(movements.Where(m => m.OccurredAt < start).Select(m => (m.Kind, m.Amount)));
        var closing = openingBalances + Delta(movements.Where(m => m.OccurredAt < end).Select(m => (m.Kind, m.Amount)));

        var yearMovs = movements.Where(m => m.OccurredAt >= start && m.OccurredAt < end).ToList();
        var inflows = yearMovs.Where(m => m.Kind == "inflow").Sum(m => m.Amount);
        var outflows = yearMovs.Where(m => m.Kind == "outflow").Sum(m => m.Amount);

        return new CashFlowStatement(year, opening, inflows, outflows, inflows - outflows, closing);
    }

    private async Task<Dictionary<(string Type, string Restriction), decimal>> AggregateByTypeRestrictionAsync(int year, CancellationToken ct)
    {
        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);

        var entries = await db.AccountingEntries
            .Where(e => e.CreatedAt >= start && e.CreatedAt < end && e.LedgerAccountId != null)
            .Select(e => new { e.LedgerAccountId, e.Debit, e.Credit, e.TransactionId })
            .ToListAsync(ct);
        var accounts = await db.LedgerAccounts.ToDictionaryAsync(a => a.Id, a => a, ct);
        var txFund = await db.Transactions.Select(t => new { t.Id, t.FundId }).ToDictionaryAsync(t => t.Id, t => t.FundId, ct);
        var fundRestriction = await db.Funds.ToDictionaryAsync(f => f.Id, f => f.Restriction, ct);

        var result = new Dictionary<(string, string), decimal>();
        foreach (var e in entries)
        {
            if (!accounts.TryGetValue(e.LedgerAccountId!.Value, out var a)) continue;
            var balance = a.NormalBalance == "debit" ? e.Debit - e.Credit : e.Credit - e.Debit;

            var restriction = "free";
            if (txFund.TryGetValue(e.TransactionId, out var fid) && fid is { } f
                && fundRestriction.TryGetValue(f, out var r))
                restriction = r;

            var key = (a.Type, restriction);
            result[key] = result.GetValueOrDefault(key) + balance;
        }
        return result;
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
