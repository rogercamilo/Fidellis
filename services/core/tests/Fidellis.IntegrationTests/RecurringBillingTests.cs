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

/// <summary>Testes da engine de recorrência + dunning (EF InMemory, fake gateway, fake clock).</summary>
public class RecurringBillingTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_1", "ch_1", "pending", "00020126PIX", "https://qr/ch_1", null));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private static readonly DateTimeOffset T0 = new(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant()
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return t;
    }

    private static TenantDbContext TDb(ITenantContext t, string db) =>
        new(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);

    private static CatalogDbContext CDb(string db) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(db).Options);

    private static RecurringBillingService Service(TenantDbContext tdb, CatalogDbContext cdb, ITenantContext tenant, IClock clock)
    {
        var checkout = new DonationCheckoutService(tdb, cdb, new FakeGateway(), tenant);
        var notifier = new LogNotifier(NullLogger<LogNotifier>.Instance);
        var options = new BillingOptions { DunningDays = [1, 3, 5], CycleExpirySeconds = 3600 };
        return new RecurringBillingService(tdb, cdb, checkout, notifier, options, clock, NullLogger<RecurringBillingService>.Instance);
    }

    [Theory]
    [InlineData("2026-03-03", 5, "2026-03-05")] // mesmo mês, dia futuro
    [InlineData("2026-03-10", 5, "2026-04-05")] // dia já passou -> próximo mês
    [InlineData("2026-02-15", 31, "2026-02-28")] // clamp p/ fim de mês
    public void NextChargeDate_computes_monthly_date(string from, int day, string expected)
    {
        var result = RecurringBillingService.NextChargeDate(day, DateTimeOffset.Parse(from + "T12:00:00Z"));
        Assert.Equal(DateOnly.Parse(expected), DateOnly.FromDateTime(result.UtcDateTime));
    }

    [Fact]
    public async Task Billing_cycle_creates_a_pix_charge_for_due_pledge()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = TDb(tenant, $"t_{id}");
        var cdb = CDb($"c_{id}");
        var donor = new Donor { Name = "Ana", Email = "ana@x.org", Document = "12345678900" };
        tdb.Donors.Add(donor);
        tdb.RecurringDonations.Add(new RecurringDonation
        {
            OrganizationId = Guid.NewGuid(), DonorId = donor.Id, Amount = 100m, DayOfMonth = 10,
            Status = "active", NextChargeAt = T0.AddMinutes(-1),
        });
        await tdb.SaveChangesAsync();

        var created = await Service(tdb, cdb, tenant, new FixedClock(T0)).RunBillingCycleAsync();

        Assert.Equal(1, created);
        var cycle = await tdb.Donations.SingleAsync();
        Assert.Equal("pending", cycle.Status);
        Assert.NotNull(cycle.RecurringDonationId);
        Assert.Equal("ch_1", cycle.PspChargeId);
        Assert.Equal(1, await cdb.PspOrders.CountAsync());
    }

    [Fact]
    public async Task Dunning_schedules_retry_on_first_failure()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = TDb(tenant, $"t_{id}");
        var cdb = CDb($"c_{id}");
        var r = new RecurringDonation
        {
            OrganizationId = Guid.NewGuid(), DonorId = Guid.NewGuid(), Amount = 100m, DayOfMonth = 10,
            Status = "active", NextChargeAt = T0, Attempt = 0,
        };
        tdb.RecurringDonations.Add(r);
        tdb.Donations.Add(new Donation
        {
            OrganizationId = r.OrganizationId, Amount = 100m, Method = "pix", Status = "pending",
            RecurringDonationId = r.Id, ExpiresAt = T0.AddMinutes(-1),
        });
        await tdb.SaveChangesAsync();

        var affected = await Service(tdb, cdb, tenant, new FixedClock(T0)).RunDunningAsync();

        Assert.Equal(1, affected);
        var updated = await tdb.RecurringDonations.SingleAsync();
        Assert.Equal("active", updated.Status);
        Assert.Equal(1, updated.Attempt);
        Assert.Equal(T0.AddDays(1), updated.NextChargeAt);
        Assert.Equal("expired", (await tdb.Donations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Dunning_marks_past_due_after_last_attempt()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = TDb(tenant, $"t_{id}");
        var cdb = CDb($"c_{id}");
        var r = new RecurringDonation
        {
            OrganizationId = Guid.NewGuid(), DonorId = Guid.NewGuid(), Amount = 100m, DayOfMonth = 10,
            Status = "active", NextChargeAt = T0, Attempt = 3, // já esgotou D+1,D+3,D+5
        };
        tdb.RecurringDonations.Add(r);
        tdb.Donations.Add(new Donation
        {
            OrganizationId = r.OrganizationId, Amount = 100m, Method = "pix", Status = "pending",
            RecurringDonationId = r.Id, ExpiresAt = T0.AddMinutes(-1),
        });
        await tdb.SaveChangesAsync();

        await Service(tdb, cdb, tenant, new FixedClock(T0)).RunDunningAsync();

        Assert.Equal("past_due", (await tdb.RecurringDonations.SingleAsync()).Status);
    }

    [Fact]
    public async Task Paid_cycle_resets_dunning_and_schedules_next_month()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = TDb(tenant, $"t_{id}");
        var r = new RecurringDonation
        {
            OrganizationId = Guid.NewGuid(), DonorId = Guid.NewGuid(), Amount = 100m, DayOfMonth = 10,
            Status = "active", NextChargeAt = T0, Attempt = 2,
        };
        tdb.RecurringDonations.Add(r);
        tdb.Donations.Add(new Donation
        {
            OrganizationId = r.OrganizationId, Amount = 100m, Method = "pix", Status = "pending",
            RecurringDonationId = r.Id, PspChargeId = "ch_1",
        });
        await tdb.SaveChangesAsync();

        var processor = new WebhookProcessor(tdb, new FakeGateway(), new FixedClock(T0), NullLogger<WebhookProcessor>.Instance);
        await processor.ProcessAsync(new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid"), "{}");

        var updated = await tdb.RecurringDonations.SingleAsync();
        Assert.Equal("active", updated.Status);
        Assert.Equal(0, updated.Attempt);
        Assert.True(updated.NextChargeAt > T0);
    }
}
