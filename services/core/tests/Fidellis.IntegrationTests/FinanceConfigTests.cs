using Fidellis.Infrastructure.Configuration;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Configurabilidade financeira (Onda 1 inc.1.1): seeding, nomenclatura e jornada de conversão.</summary>
public class FinanceConfigTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_1", "ch_1", "pending", "qr", null, null));
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_1", "ch_1", "pending", "34191", "barcode", "http://pdf", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static RecurringBillingService Billing(TenantDbContext tdb, IClock clock)
    {
        var checkout = new DonationCheckoutService(tdb, CatalogInMemory(), new FakeGateway(), Tenant(tdb));
        return new RecurringBillingService(tdb, CatalogInMemory(), checkout,
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), new BillingOptions(), clock,
            NullLogger<RecurringBillingService>.Instance);
    }

    private static ITenantContext Tenant(TenantDbContext tdb)
    {
        var t = new TenantContext(); t.SetTenant("diocese-sp"); return t;
    }

    private static CatalogDbContext CatalogInMemory() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase($"cat_{Guid.NewGuid()}").Options);

    [Fact]
    public async Task Seeder_creates_settings_and_default_donor_types()
    {
        var tdb = TDb($"cfg_{Guid.NewGuid()}");
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync();

        var settings = await tdb.FinanceSettings.SingleAsync();
        Assert.Equal("Dízimo", settings.RecurringLabel);
        Assert.Equal("Oferta", settings.OnetimeLabel);

        var types = await tdb.DonorTypes.ToListAsync();
        Assert.Equal(2, types.Count);
        Assert.Single(types, t => t.IsRecurringDefault && t.Name == FinanceConfigSeeder.RecurringDonorTypeName);
    }

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        var tdb = TDb($"cfg_{Guid.NewGuid()}");
        var seeder = new FinanceConfigSeeder(tdb);
        await seeder.EnsureDefaultsAsync();
        await seeder.EnsureDefaultsAsync();

        Assert.Equal(1, await tdb.FinanceSettings.CountAsync());
        Assert.Equal(2, await tdb.DonorTypes.CountAsync());
    }

    [Fact]
    public async Task Creating_a_pledge_marks_donor_conversion_and_default_type()
    {
        var tdb = TDb($"cfg_{Guid.NewGuid()}");
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync();
        var recurringType = await tdb.DonorTypes.FirstAsync(t => t.IsRecurringDefault);

        var donor = new Donor { Name = "Ana", Email = "ana@ex.com" };
        tdb.Donors.Add(donor);
        await tdb.SaveChangesAsync();
        Assert.Null(donor.ConvertedAt);

        await Billing(tdb, new FixedClock(T0)).CreatePledgeAsync(Guid.NewGuid(), donor.Id, 50m, 10, chargeToday: false);

        var reloaded = await tdb.Donors.SingleAsync(d => d.Id == donor.Id);
        Assert.Equal(T0, reloaded.ConvertedAt);
        Assert.Equal(recurringType.Id, reloaded.DonorTypeId);
    }
}
