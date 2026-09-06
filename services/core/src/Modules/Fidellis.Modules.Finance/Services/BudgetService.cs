using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>Linha de previsto × realizado do orçamento.</summary>
public sealed record BudgetActual(
    Guid BudgetId, int Year, string Kind, Guid? CostCenterId, Guid? ProjectId, Guid? FundId,
    decimal Budgeted, decimal Realized, decimal Variance, bool OverBudget);

/// <summary>
/// Orçamento (Onda 3 inc.3.3): CRUD por dimensão/ano e <b>previsto × realizado</b> por
/// <b>competência</b> (decisão D4) — o realizado soma as movimentações contábeis (transactions) do ano
/// por dimensão. Dimensão nula no orçamento não restringe aquela dimensão no realizado. Roda no schema
/// do tenant.
/// </summary>
public sealed class BudgetService(TenantDbContext db)
{
    public async Task<Budget> CreateAsync(
        int year, string kind, decimal amount, Guid? costCenterId, Guid? projectId, Guid? fundId, CancellationToken ct = default)
    {
        if (amount <= 0) throw new ArgumentException("O valor deve ser positivo.");
        var k = kind is "revenue" or "expense" ? kind : throw new ArgumentException("kind deve ser 'revenue' ou 'expense'.");

        var budget = new Budget { Year = year, Kind = k, Amount = amount, CostCenterId = costCenterId, ProjectId = projectId, FundId = fundId };
        db.Budgets.Add(budget);
        await db.SaveChangesAsync(ct);
        return budget;
    }

    public Task<List<Budget>> ListAsync(int? year, CancellationToken ct = default)
    {
        var q = db.Budgets.Where(b => b.Active);
        if (year is { } y) q = q.Where(b => b.Year == y);
        return q.OrderBy(b => b.Year).ThenBy(b => b.Kind).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BudgetActual>> ActualAsync(int year, CancellationToken ct = default)
    {
        var budgets = await db.Budgets.Where(b => b.Active && b.Year == year).ToListAsync(ct);
        if (budgets.Count == 0) return [];

        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);
        var txs = await db.Transactions
            .Where(t => t.CreatedAt >= start && t.CreatedAt < end)
            .Select(t => new { t.Kind, t.Amount, t.CostCenterId, t.ProjectId, t.FundId })
            .ToListAsync(ct);

        var result = new List<BudgetActual>(budgets.Count);
        foreach (var b in budgets)
        {
            var side = b.Kind == "revenue" ? "credit" : "debit";
            var realized = txs
                .Where(t => t.Kind == side
                    && (b.CostCenterId is null || t.CostCenterId == b.CostCenterId)
                    && (b.ProjectId is null || t.ProjectId == b.ProjectId)
                    && (b.FundId is null || t.FundId == b.FundId))
                .Sum(t => t.Amount);

            var variance = b.Amount - realized;
            var overBudget = b.Kind == "expense" && realized > b.Amount;
            result.Add(new BudgetActual(b.Id, b.Year, b.Kind, b.CostCenterId, b.ProjectId, b.FundId, b.Amount, realized, variance, overBudget));
        }

        return result;
    }
}
