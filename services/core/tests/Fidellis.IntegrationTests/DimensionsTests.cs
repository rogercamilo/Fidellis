using Fidellis.Infrastructure.Dimensions;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Dimensões gerenciais (Onda 1): seeding default idempotente + propagação de dimensões.</summary>
public class DimensionsTests
{
    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Seeder_creates_default_cost_center_and_free_fund()
    {
        var tdb = TDb($"dim_{Guid.NewGuid()}");
        await new DimensionsSeeder(tdb).EnsureDefaultsAsync();

        var cc = await tdb.CostCenters.SingleAsync();
        Assert.Equal(DimensionsSeeder.DefaultCostCenterCode, cc.Code);
        Assert.True(cc.IsDefault);

        var fund = await tdb.Funds.SingleAsync();
        Assert.Equal(DimensionsSeeder.DefaultFundCode, fund.Code);
        Assert.Equal("free", fund.Restriction);
        Assert.True(fund.IsDefault);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        var tdb = TDb($"dim_{Guid.NewGuid()}");
        var seeder = new DimensionsSeeder(tdb);
        await seeder.EnsureDefaultsAsync();
        await seeder.EnsureDefaultsAsync();

        Assert.Equal(1, await tdb.CostCenters.CountAsync());
        Assert.Equal(1, await tdb.Funds.CountAsync());
    }

    [Fact]
    public async Task Restricted_fund_keeps_its_purpose()
    {
        var tdb = TDb($"dim_{Guid.NewGuid()}");
        tdb.Funds.Add(new Fund { Code = "OBRA", Name = "Obra social", Restriction = "restricted", Purpose = "Construção do centro comunitário" });
        await tdb.SaveChangesAsync();

        var fund = await tdb.Funds.SingleAsync(f => f.Code == "OBRA");
        Assert.Equal("restricted", fund.Restriction);
        Assert.False(fund.IsDefault);
        Assert.Equal("Construção do centro comunitário", fund.Purpose);
    }
}
