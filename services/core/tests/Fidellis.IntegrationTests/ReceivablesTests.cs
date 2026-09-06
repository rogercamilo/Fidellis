using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Contas a Receber (Onda 2 inc.2.1): criação, baixa manual/automática e aging.</summary>
public class ReceivablesTests
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
    public async Task Partial_then_full_settlement_updates_status()
    {
        var tdb = TDb($"ar_{Guid.NewGuid()}");
        var svc = new ReceivablesService(tdb, new FixedClock(T0));
        var r = await svc.CreateAsync(Guid.NewGuid(), 100m, new DateOnly(2026, 6, 10), "pledge", null, "Dízimo jun", null, null, null);

        await svc.SettleAsync(r.Id, 40m, null);
        Assert.Equal("partial", (await tdb.Receivables.SingleAsync()).Status);

        await svc.SettleAsync(r.Id, 60m, null);
        var settled = await tdb.Receivables.SingleAsync();
        Assert.Equal("received", settled.Status);
        Assert.Equal(100m, settled.ReceivedAmount);
    }

    [Fact]
    public async Task Reconciling_a_linked_donation_settles_the_receivable()
    {
        var tdb = TDb($"ar_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var svc = new ReceivablesService(tdb, clock);
        var org = Guid.NewGuid();
        var r = await svc.CreateAsync(org, 80m, new DateOnly(2026, 6, 1), "pledge", null, null, null, null, null);

        var donation = new Donation { OrganizationId = org, Amount = 80m, Method = "pix", Status = "paid", ReceivableId = r.Id, DonorName = "Ana" };
        tdb.Donations.Add(donation);
        await tdb.SaveChangesAsync();

        var recon = new ReconciliationService(tdb, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock);
        await recon.PostPaidAsync(donation);
        await tdb.SaveChangesAsync();

        var settled = await tdb.Receivables.SingleAsync();
        Assert.Equal("received", settled.Status);
        Assert.Equal(donation.Id, settled.DonationId);
    }

    [Fact]
    public async Task Aging_buckets_by_due_date()
    {
        var tdb = TDb($"ar_{Guid.NewGuid()}");
        var svc = new ReceivablesService(tdb, new FixedClock(T0)); // hoje = 2026-05-20
        await svc.CreateAsync(Guid.NewGuid(), 100m, new DateOnly(2026, 6, 1), "pledge", null, null, null, null, null);   // a vencer
        await svc.CreateAsync(Guid.NewGuid(), 50m, new DateOnly(2026, 5, 10), "pledge", null, null, null, null, null);    // vencido 10d
        await svc.CreateAsync(Guid.NewGuid(), 30m, new DateOnly(2026, 3, 1), "pledge", null, null, null, null, null);     // vencido 80d

        var aging = await svc.AgingAsync();
        Assert.Equal(100m, aging.NotDue);
        Assert.Equal(50m, aging.Overdue1To30);
        Assert.Equal(30m, aging.Overdue60Plus);
        Assert.Equal(180m, aging.TotalOutstanding);
    }
}
