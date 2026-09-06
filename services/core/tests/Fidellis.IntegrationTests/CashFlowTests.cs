using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Fluxo de caixa projetado (Onda 2 inc.2.4): D+30/60/90.</summary>
public class CashFlowTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 0, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Projection_combines_balance_receivables_and_payables()
    {
        var tdb = TDb($"cf_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var org = Guid.NewGuid();

        await treasury.CreateAccountAsync(org, "Banco", "bank", 1000m);                 // saldo inicial 1000
        tdb.Receivables.Add(new Receivable { OrganizationId = org, Amount = 300m, DueDate = new DateOnly(2026, 6, 1), Status = "open" });   // +300 (dentro de 30d)
        tdb.Payables.Add(new Payable { PayeeId = Guid.NewGuid(), Description = "Luz", Amount = 200m, DueDate = new DateOnly(2026, 6, 5), Status = "approved" }); // -200 (dentro de 30d)
        await tdb.SaveChangesAsync();

        var svc = new CashFlowService(tdb, treasury, clock);
        var projection = await svc.ProjectAsync([org]);

        var d30 = projection.First(p => p.HorizonDays == 30);
        Assert.Equal(1000m, d30.Opening);
        Assert.Equal(300m, d30.ExpectedInflows);
        Assert.Equal(200m, d30.ExpectedOutflows);
        Assert.Equal(1100m, d30.Projected); // 1000 + 300 - 200
    }

    [Fact]
    public async Task Awaiting_approval_payables_are_not_projected()
    {
        var tdb = TDb($"cf_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var org = Guid.NewGuid();
        await treasury.CreateAccountAsync(org, "Banco", "bank", 500m);
        tdb.Payables.Add(new Payable { PayeeId = Guid.NewGuid(), Description = "Pendente", Amount = 100m, DueDate = new DateOnly(2026, 6, 1), Status = "awaiting_approval" });
        await tdb.SaveChangesAsync();

        var d30 = (await new CashFlowService(tdb, treasury, clock).ProjectAsync([org])).First(p => p.HorizonDays == 30);
        Assert.Equal(0m, d30.ExpectedOutflows); // não entra na projeção (D5)
        Assert.Equal(500m, d30.Projected);
    }

    [Fact]
    public async Task Recurring_donations_accrue_more_cycles_on_longer_horizons()
    {
        var tdb = TDb($"cf_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var org = Guid.NewGuid();
        await treasury.CreateAccountAsync(org, "Banco", "bank", 0m);
        tdb.RecurringDonations.Add(new RecurringDonation { OrganizationId = org, DonorId = Guid.NewGuid(), Amount = 100m, Status = "active", NextChargeAt = T0.AddDays(5) });
        await tdb.SaveChangesAsync();

        var projection = await new CashFlowService(tdb, treasury, clock).ProjectAsync([org]);
        var d30 = projection.First(p => p.HorizonDays == 30);
        var d90 = projection.First(p => p.HorizonDays == 90);

        Assert.True(d90.ExpectedInflows > d30.ExpectedInflows); // mais ciclos em 90 dias
    }
}
