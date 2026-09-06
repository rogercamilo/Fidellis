using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Contas a Pagar — base (Onda 2 inc.2.2): credores, títulos, rateio.</summary>
public class PayablesTests
{
    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Payable_is_created_awaiting_approval()
    {
        var tdb = TDb($"ap_{Guid.NewGuid()}");
        var svc = new PayablesService(tdb);
        var payee = await svc.CreatePayeeAsync("Energia SA", "12345678000199", null, "supplier");

        var p = await svc.CreatePayableAsync(payee.Id, 300m, new DateOnly(2026, 6, 10), "Conta de luz",
            null, null, null, null, null, null, null);

        Assert.Equal("awaiting_approval", p.Status);
        Assert.Equal(300m, p.Amount);
    }

    [Fact]
    public async Task Rateio_must_equal_the_total()
    {
        var tdb = TDb($"ap_{Guid.NewGuid()}");
        var svc = new PayablesService(tdb);
        var payee = await svc.CreatePayeeAsync("Energia SA", null, null, "supplier");

        var bad = new List<PayableAllocationInput> { new(100m), new(150m) }; // soma 250 ≠ 300
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreatePayableAsync(payee.Id, 300m, new DateOnly(2026, 6, 10), "Luz", null, null, null, null, null, bad, null));

        var ok = new List<PayableAllocationInput> { new(200m), new(100m) }; // soma 300
        var p = await svc.CreatePayableAsync(payee.Id, 300m, new DateOnly(2026, 6, 10), "Luz", null, null, null, null, null, ok, null);
        Assert.Equal(2, await tdb.PayableAllocations.CountAsync(a => a.PayableId == p.Id));
    }

    [Fact]
    public async Task Create_payable_rejects_unknown_payee()
    {
        var tdb = TDb($"ap_{Guid.NewGuid()}");
        var svc = new PayablesService(tdb);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreatePayableAsync(Guid.NewGuid(), 100m, new DateOnly(2026, 6, 1), "X", null, null, null, null, null, null, null));
    }

    [Fact]
    public async Task Cancel_sets_status_canceled()
    {
        var tdb = TDb($"ap_{Guid.NewGuid()}");
        var svc = new PayablesService(tdb);
        var payee = await svc.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        var p = await svc.CreatePayableAsync(payee.Id, 50m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, null);

        var canceled = await svc.CancelAsync(p.Id);
        Assert.Equal("canceled", canceled!.Status);
    }
}
