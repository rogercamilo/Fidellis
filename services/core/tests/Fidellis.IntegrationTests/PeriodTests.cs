using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Configuration;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Fechamento de período (Onda 2 inc.2.6): fechar, reabrir (admin) e guarda de lançamento.</summary>
public class PeriodTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Close_then_period_is_closed_for_dates_in_that_month()
    {
        var tdb = TDb($"pd_{Guid.NewGuid()}");
        var svc = new PeriodService(tdb, new FixedClock(T0));
        await svc.CloseAsync(2026, 5, Guid.NewGuid());

        Assert.True(await svc.IsClosedForDateAsync(new DateOnly(2026, 5, 15)));
        Assert.False(await svc.IsClosedForDateAsync(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task Reopen_requires_admin()
    {
        var tdb = TDb($"pd_{Guid.NewGuid()}");
        var svc = new PeriodService(tdb, new FixedClock(T0));
        await svc.CloseAsync(2026, 5, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ReopenAsync(2026, 5, FinanceRoles.Treasurer));

        var reopened = await svc.ReopenAsync(2026, 5, FinanceRoles.Admin);
        Assert.Equal("open", reopened!.Status);
    }

    [Fact]
    public async Task Paying_in_a_closed_period_is_blocked()
    {
        var tdb = TDb($"pd_{Guid.NewGuid()}");
        var clock = new FixedClock(T0); // maio/2026
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync();
        var periods = new PeriodService(tdb, clock);
        var payables = new PayablesService(tdb, clock, new ChartOfAccountsSeeder(tdb), periods);
        var approvals = new ApprovalService(tdb, clock);

        var payee = await payables.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        var p = await payables.CreatePayableAsync(payee.Id, 100m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());
        await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "treasurer");

        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 500m);

        await periods.CloseAsync(2026, 5, Guid.NewGuid()); // fecha o mês corrente
        await Assert.ThrowsAsync<InvalidOperationException>(() => payables.PayAsync(p.Id, acc.Id));
    }
}
