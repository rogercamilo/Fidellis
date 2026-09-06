using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Orçamento (Onda 3 inc.3.3): CRUD + previsto × realizado por dimensão.</summary>
public class BudgetTests
{
    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    // CreatedAt (competência) é sempre "agora" (ano corrente = 2026); o ano do orçamento usa esse ano.
    private static readonly int CurrentYear = DateTimeOffset.UtcNow.Year;

    private static void AddTx(TenantDbContext tdb, string kind, decimal amount, Guid? cc = null, Guid? fund = null)
        => tdb.Transactions.Add(new Transaction { AccountId = Guid.NewGuid(), Kind = kind, Amount = amount, Description = "x", CostCenterId = cc, FundId = fund });

    [Fact]
    public async Task Actual_sums_realized_expense_and_flags_overbudget()
    {
        var tdb = TDb($"bud_{Guid.NewGuid()}");
        var cc = Guid.NewGuid();
        var svc = new BudgetService(tdb);
        await svc.CreateAsync(CurrentYear, "expense", 1000m, cc, null, null);

        // Realizado (débitos) no centro de custo — fundo diferente não deve importar (dim nula no orçamento).
        AddTx(tdb, "debit", 700m, cc, Guid.NewGuid());
        AddTx(tdb, "debit", 500m, cc, null);
        AddTx(tdb, "debit", 999m, Guid.NewGuid()); // outro centro de custo → ignorado
        await tdb.SaveChangesAsync();

        var actual = await svc.ActualAsync(CurrentYear);
        var row = Assert.Single(actual);
        Assert.Equal(1000m, row.Budgeted);
        Assert.Equal(1200m, row.Realized);   // 700 + 500
        Assert.True(row.OverBudget);
        Assert.Equal(-200m, row.Variance);
    }

    [Fact]
    public async Task Actual_sums_only_the_matching_side()
    {
        var tdb = TDb($"bud_{Guid.NewGuid()}");
        var svc = new BudgetService(tdb);
        await svc.CreateAsync(CurrentYear, "revenue", 5000m, null, null, null);

        AddTx(tdb, "credit", 3000m); // receita
        AddTx(tdb, "debit", 400m);   // despesa (lado diferente → ignorado p/ receita)
        await tdb.SaveChangesAsync();

        var row = Assert.Single(await svc.ActualAsync(CurrentYear));
        Assert.Equal(3000m, row.Realized); // só créditos
        Assert.False(row.OverBudget);      // receita não sinaliza estouro
    }

    [Fact]
    public async Task Other_year_has_no_actual_rows()
    {
        var tdb = TDb($"bud_{Guid.NewGuid()}");
        var svc = new BudgetService(tdb);
        await svc.CreateAsync(2099, "expense", 100m, null, null, null); // ano sem transações

        var actual = await svc.ActualAsync(2099);
        Assert.Equal(0m, Assert.Single(actual).Realized);
    }

    [Fact]
    public async Task Create_rejects_invalid_kind()
    {
        var tdb = TDb($"bud_{Guid.NewGuid()}");
        var svc = new BudgetService(tdb);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(2026, "outro", 100m, null, null, null));
    }
}
