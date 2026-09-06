using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>DFC (método direto) + trabalho voluntário (Onda 4 inc.4.2).</summary>
public class CashFlowVolunteerTests
{
    private static readonly int Year = DateTimeOffset.UtcNow.Year;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Cashflow_direct_method_matches_movements()
    {
        var tdb = TDb($"cf_{Guid.NewGuid()}");
        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 1000m);

        var inYear = new DateTimeOffset(Year, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var priorYear = new DateTimeOffset(Year - 1, 12, 1, 0, 0, 0, TimeSpan.Zero);
        tdb.TreasuryMovements.AddRange(
            new TreasuryMovement { AccountId = acc.Id, Kind = "inflow", Amount = 500m, OccurredAt = inYear },
            new TreasuryMovement { AccountId = acc.Id, Kind = "outflow", Amount = 200m, OccurredAt = inYear },
            new TreasuryMovement { AccountId = acc.Id, Kind = "inflow", Amount = 100m, OccurredAt = priorYear });
        await tdb.SaveChangesAsync();

        var dfc = await new StatementsService(tdb).CashFlowAsync(Year);
        Assert.Equal(1100m, dfc.OpeningCash);   // 1000 abertura + 100 do ano anterior
        Assert.Equal(500m, dfc.Inflows);
        Assert.Equal(200m, dfc.Outflows);
        Assert.Equal(300m, dfc.NetCash);
        Assert.Equal(1400m, dfc.ClosingCash);   // 1100 + 300
    }

    [Fact]
    public async Task Volunteer_work_posts_fair_value_to_revenue_and_expense()
    {
        var tdb = TDb($"vw_{Guid.NewGuid()}");
        var svc = new VolunteerWorkService(tdb, new ChartOfAccountsSeeder(tdb));

        var v = await svc.RecordAsync(Guid.NewGuid(), "Mutirão da obra social", 400m, new DateOnly(Year, 4, 1), null, null, null);

        Assert.NotNull(v.TransactionId);
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());

        // Aparece na DRP como receita e despesa (resultado líquido zero do voluntariado).
        var drp = await new StatementsService(tdb).IncomeAsync(Year);
        Assert.Equal(400m, drp.Revenues);
        Assert.Equal(400m, drp.Expenses);
        Assert.Equal(0m, drp.Surplus);
    }
}
