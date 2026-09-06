using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Testes de serviço com EF InMemory e um <see cref="IPaymentGateway"/> fake (sem PSP ao vivo).</summary>
public class FinanceServiceTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public PixOrderResult Order = new("or_1", "ch_1", "pending", "00020126PIX", "https://qr/ch_1", DateTimeOffset.UtcNow.AddHours(1));
        public BoletoOrderResult Boleto = new("or_1", "ch_1", "pending", "34191", "barcode", "http://pdf", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)));
        public ChargeStatusResult Charge = new("ch_1", "paid", DateTimeOffset.UtcNow);
        public CreateRecipientResult Recipient = new("rp_1", "active");

        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default) => Task.FromResult(Order);
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default) => Task.FromResult(Boleto);
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default) => Task.FromResult(Charge);
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default) => Task.FromResult(Recipient);
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = now;
    }

    private static TenantContext Tenant()
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return t;
    }

    private static TenantDbContext NewTenantDb(ITenantContext t, string db) =>
        new(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);

    private static CatalogDbContext NewCatalogDb(string db) =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(db).Options);

    private static WebhookProcessor Processor(TenantDbContext tdb, IPaymentGateway gw, IClock clock) =>
        new(tdb, gw, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock, NullLogger<WebhookProcessor>.Instance);

    [Fact]
    public async Task Checkout_persists_pending_donation_order_and_catalog_index()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = NewTenantDb(tenant, $"t_{id}");
        var cdb = NewCatalogDb($"c_{id}");
        var svc = new DonationCheckoutService(tdb, cdb, new FakeGateway(), tenant);
        var orgId = Guid.NewGuid();

        var result = await svc.CreateAsync(new CheckoutCommand(orgId, 100m, "Ana", "ana@x.org", "12345678900"));

        Assert.Equal("00020126PIX", result.QrCode);
        Assert.Equal("pending", result.Status);

        var donation = await tdb.Donations.SingleAsync();
        Assert.Equal("or_1", donation.PspOrderId);
        Assert.Equal("ch_1", donation.PspChargeId);

        var index = await cdb.PspOrders.SingleAsync();
        Assert.Equal("or_1", index.ProviderOrderId);
        Assert.Equal("diocese-sp", index.TenantSlug);
        Assert.Equal(donation.Id, index.DonationId);
    }

    [Fact]
    public async Task Webhook_confirms_payment_with_balanced_double_entry()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = NewTenantDb(tenant, $"t_{id}");
        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 100m, Method = "pix", Status = "pending", PspChargeId = "ch_1",
        });
        await tdb.SaveChangesAsync();

        var processor = Processor(tdb, new FakeGateway(), new FixedClock(DateTimeOffset.UtcNow));
        var evt = new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid");

        var processed = await processor.ProcessAsync(evt, "{}");

        Assert.True(processed);
        Assert.Equal("paid", (await tdb.Donations.SingleAsync()).Status);
        Assert.Equal(1, await tdb.Transactions.CountAsync());

        var entries = await tdb.AccountingEntries.ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Equal(entries.Sum(e => e.Debit), entries.Sum(e => e.Credit));
        Assert.Equal(100m, entries.Sum(e => e.Debit));
    }

    [Fact]
    public async Task Webhook_is_idempotent_on_duplicate_event()
    {
        var id = Guid.NewGuid().ToString();
        var tenant = Tenant();
        var tdb = NewTenantDb(tenant, $"t_{id}");
        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 100m, Method = "pix", Status = "pending", PspChargeId = "ch_1",
        });
        await tdb.SaveChangesAsync();

        var processor = Processor(tdb, new FakeGateway(), new FixedClock(DateTimeOffset.UtcNow));
        var evt = new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid");

        Assert.True(await processor.ProcessAsync(evt, "{}"));
        Assert.False(await processor.ProcessAsync(evt, "{}")); // duplicado -> no-op

        Assert.Equal(1, await tdb.Transactions.CountAsync());
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());
    }
}
