using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Offboarding do vínculo federado (#80 / ADR-0013): pausa/encerra a recorrência do membro, com aviso.</summary>
public class IntegrationOffboardTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_1", "ch_1", "pending", "qr", "https://qr", null));
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_1", "ch_1", "pending", "l", "b", "u", null));
        public Task<CardChargeResult> CreateCardOrderAsync(CreateCardOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new CardChargeResult("or_1", "ch_1", "paid", null, "visa", "1234"));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private static TenantDbContext TDb(ITenantContext t, string db) =>
        new(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);

    private static IntegrationOffboardService Service(TenantDbContext tdb)
    {
        var tc = new TenantContext(); tc.SetTenant("diocese-sp");
        var cdb = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase($"c_{Guid.NewGuid()}").Options);
        var checkout = new DonationCheckoutService(tdb, cdb, new FakeGateway(), tc);
        var billing = new RecurringBillingService(tdb, cdb, checkout, new LogNotifier(NullLogger<LogNotifier>.Instance),
            new BillingOptions { DunningDays = [1, 3, 5], CycleExpirySeconds = 3600 },
            new FixedClock(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero)), NullLogger<RecurringBillingService>.Instance);
        return new IntegrationOffboardService(tdb, billing, new MessageOutbox(tdb));
    }

    private static async Task<Donor> SeedMemberWithPledgesAsync(TenantDbContext db, int pledges)
    {
        var tc = new TenantContext(); tc.SetTenant("diocese-sp");
        var member = new Donor { Name = "João", Email = "joao@ex.com", Document = "12345678900", IsMember = true, Source = "formattio", ExternalId = "fmnd_1" };
        db.Donors.Add(member);
        for (var i = 0; i < pledges; i++)
            db.RecurringDonations.Add(new RecurringDonation
            {
                OrganizationId = Guid.NewGuid(), DonorId = member.Id, Amount = 100m, DayOfMonth = 5,
                Status = "active", NextChargeAt = DateTimeOffset.UtcNow, EntryType = EntryTypes.Tithe,
            });
        await db.SaveChangesAsync();
        return member;
    }

    [Fact]
    public async Task Offboard_pauses_recurrences_and_notifies()
    {
        var tc = new TenantContext(); tc.SetTenant("diocese-sp");
        var db = TDb(tc, $"off_{Guid.NewGuid()}");
        await SeedMemberWithPledgesAsync(db, 2);

        var result = await Service(db).OffboardAsync("fmnd_1", permanent: false);

        Assert.Equal((2, "paused"), result);
        Assert.Equal(2, await db.RecurringDonations.CountAsync(r => r.Status == "paused"));
        Assert.Equal(1, await db.Messages.CountAsync()); // um aviso (dedupe por membro+ação)
    }

    [Fact]
    public async Task Offboard_permanent_cancels_recurrences()
    {
        var tc = new TenantContext(); tc.SetTenant("diocese-sp");
        var db = TDb(tc, $"off_{Guid.NewGuid()}");
        await SeedMemberWithPledgesAsync(db, 1);

        var result = await Service(db).OffboardAsync("fmnd_1", permanent: true);

        Assert.Equal((1, "canceled"), result);
        var r = await db.RecurringDonations.SingleAsync();
        Assert.Equal("canceled", r.Status);
        Assert.NotNull(r.CanceledAt);
    }

    [Fact]
    public async Task Offboard_unknown_member_returns_null()
    {
        var tc = new TenantContext(); tc.SetTenant("diocese-sp");
        var db = TDb(tc, $"off_{Guid.NewGuid()}");
        Assert.Null(await Service(db).OffboardAsync("inexistente", permanent: false));
    }
}
