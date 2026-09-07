using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Régua de Contas a Receber (DT-06): lembretes de títulos a vencer/vencidos via outbox.</summary>
public class ReceivableRemindersTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero); // hoje

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static ReceivablesReminderService Svc(TenantDbContext tdb, IClock clock)
        => new(tdb, new MessageOutbox(tdb), clock);

    private static async Task<Donor> DonorAsync(TenantDbContext tdb, string? email, bool optOut = false)
    {
        var donor = new Donor { Name = "Ana", Email = email, Document = "111", ContactOptOut = optOut };
        tdb.Donors.Add(donor);
        await tdb.SaveChangesAsync();
        return donor;
    }

    private static async Task ReceivableAsync(TenantDbContext tdb, IClock clock, Guid donorId, DateOnly due)
        => await new ReceivablesService(tdb, clock)
            .CreateAsync(Guid.NewGuid(), 100m, due, "pledge", donorId, "Dízimo", null, null, null);

    [Fact]
    public async Task Due_soon_enqueues_once_and_is_deduped()
    {
        var tdb = TDb($"arr_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var donor = await DonorAsync(tdb, "ana@ex.com");
        await ReceivableAsync(tdb, clock, donor.Id, new DateOnly(2026, 5, 22)); // vence em 2 dias

        Assert.Equal(1, await Svc(tdb, clock).EnqueueRemindersAsync());
        Assert.Equal(0, await Svc(tdb, clock).EnqueueRemindersAsync()); // dedupe: nada novo

        var msg = await tdb.Messages.SingleAsync();
        Assert.Equal(MessageTemplates.ReceivableDueSoon, msg.EventType);
        Assert.Equal("ana@ex.com", msg.ToAddress);
    }

    [Fact]
    public async Task Overdue_enqueues_reminder()
    {
        var tdb = TDb($"arr_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var donor = await DonorAsync(tdb, "ana@ex.com");
        await ReceivableAsync(tdb, clock, donor.Id, new DateOnly(2026, 5, 1)); // vencido

        Assert.Equal(1, await Svc(tdb, clock).EnqueueRemindersAsync());
        Assert.Equal(MessageTemplates.ReceivableOverdue, (await tdb.Messages.SingleAsync()).EventType);
    }

    [Fact]
    public async Task Not_yet_in_window_is_skipped()
    {
        var tdb = TDb($"arr_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var donor = await DonorAsync(tdb, "ana@ex.com");
        await ReceivableAsync(tdb, clock, donor.Id, new DateOnly(2026, 6, 30)); // longe do vencimento

        Assert.Equal(0, await Svc(tdb, clock).EnqueueRemindersAsync());
        Assert.Equal(0, await tdb.Messages.CountAsync());
    }

    [Fact]
    public async Task Opt_out_or_no_email_is_skipped()
    {
        var tdb = TDb($"arr_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var optOut = await DonorAsync(tdb, "opt@ex.com", optOut: true);
        var noEmail = await DonorAsync(tdb, null);
        await ReceivableAsync(tdb, clock, optOut.Id, new DateOnly(2026, 5, 1));
        await ReceivableAsync(tdb, clock, noEmail.Id, new DateOnly(2026, 5, 1));

        Assert.Equal(0, await Svc(tdb, clock).EnqueueRemindersAsync());
        Assert.Equal(0, await tdb.Messages.CountAsync());
    }
}
