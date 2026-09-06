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

/// <summary>Boleto (Onda 1 inc.1.3): payload, checkout, conciliação e expiração avulsa.</summary>
public class BoletoTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_p", "ch_p", "pending", "qr", null, null));
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_1", "ch_1", "pending",
                "34191.79001 01043.510047 91020.150008 5 84410000012345", "34195844100000123451790001010435100479102015",
                "https://pagar.me/boleto/ch_1.pdf", DateOnly.FromDateTime(new DateTime(2026, 5, 25))));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant() { var t = new TenantContext(); t.SetTenant("diocese-sp"); return t; }
    private static TenantDbContext TDb(string db, ITenantContext tenant) =>
        new(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    private static CatalogDbContext Catalog() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase($"cat_{Guid.NewGuid()}").Options);

    [Fact]
    public void Boleto_payload_builds_and_parses()
    {
        var body = PagarmePayloads.BuildBoletoOrder(new CreateBoletoOrderRequest(120m, "Ana", "ana@ex.com", "12345678900", DueInDays: 3));
        Assert.Contains("\"payment_method\":\"boleto\"", body);
        Assert.Contains("\"amount\":12000", body); // centavos

        const string resp = """
        {"id":"or_1","charges":[{"id":"ch_1","status":"pending","last_transaction":{"line":"34191.7900","barcode":"3419584","pdf":"https://x/b.pdf","due_at":"2026-05-25T00:00:00Z"}}]}
        """;
        var parsed = PagarmePayloads.ParseBoletoOrderResponse(resp);
        Assert.Equal("ch_1", parsed.ChargeId);
        Assert.Equal("34191.7900", parsed.Line);
        Assert.Equal("https://x/b.pdf", parsed.BoletoUrl);
        Assert.Equal(new DateOnly(2026, 5, 25), parsed.DueDate);
    }

    [Fact]
    public async Task Boleto_checkout_creates_pending_donation_with_boleto_fields()
    {
        var tenant = Tenant();
        var tdb = TDb($"bol_{Guid.NewGuid()}", tenant);
        var checkout = new DonationCheckoutService(tdb, Catalog(), new FakeGateway(), tenant);

        var result = await checkout.CreateAsync(new CheckoutCommand(
            Guid.NewGuid(), 120m, "Ana", "ana@ex.com", "12345678900", Method: "boleto"));

        Assert.Equal("boleto", result.Method);
        Assert.Equal("pending", result.Status);
        Assert.False(string.IsNullOrEmpty(result.BoletoLine));
        Assert.Equal("https://pagar.me/boleto/ch_1.pdf", result.BoletoUrl);
        Assert.Equal(new DateOnly(2026, 5, 25), result.DueDate);

        var d = await tdb.Donations.SingleAsync();
        Assert.Equal("boleto", d.Method);
        Assert.Equal(new DateOnly(2026, 5, 25), d.DueDate);
    }

    [Fact]
    public async Task Paid_boleto_reconciles_like_pix()
    {
        var tenant = Tenant();
        var tdb = TDb($"bol_{Guid.NewGuid()}", tenant);
        var clock = new FixedClock(T0);
        var processor = new WebhookProcessor(tdb, new FakeGateway(), new ChartOfAccountsSeeder(tdb),
            new ReceiptService(tdb, clock), new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock,
            NullLogger<WebhookProcessor>.Instance);

        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 80m, Method = "boleto", Status = "pending",
            PspChargeId = "ch_1", DonorName = "Ana",
        });
        await tdb.SaveChangesAsync();

        await processor.ProcessAsync(new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid"), "{}");

        var d = await tdb.Donations.SingleAsync();
        Assert.Equal("paid", d.Status);
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());
        Assert.Equal(1, await tdb.Receipts.CountAsync());
    }

    [Fact]
    public async Task Expiry_sweep_expires_overdue_avulsa_but_not_recurring()
    {
        var tenant = Tenant();
        var tdb = TDb($"bol_{Guid.NewGuid()}", tenant);
        var now = new DateTimeOffset(2026, 5, 30, 0, 0, 0, TimeSpan.Zero);

        var overdue = new Donation { OrganizationId = Guid.NewGuid(), Amount = 50m, Method = "boleto", Status = "pending", DueDate = new DateOnly(2026, 5, 25) };
        var future = new Donation { OrganizationId = Guid.NewGuid(), Amount = 50m, Method = "boleto", Status = "pending", DueDate = new DateOnly(2026, 6, 25) };
        var recurring = new Donation { OrganizationId = Guid.NewGuid(), Amount = 50m, Method = "pix", Status = "pending", RecurringDonationId = Guid.NewGuid(), ExpiresAt = new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero) };
        tdb.Donations.AddRange(overdue, future, recurring);
        await tdb.SaveChangesAsync();

        var expired = await new DonationExpiryService(tdb, new FixedClock(now)).ExpireOverdueAsync();

        Assert.Equal(1, expired);
        Assert.Equal("expired", (await tdb.Donations.FirstAsync(d => d.Id == overdue.Id)).Status);
        Assert.Equal("pending", (await tdb.Donations.FirstAsync(d => d.Id == future.Id)).Status);
        Assert.Equal("pending", (await tdb.Donations.FirstAsync(d => d.Id == recurring.Id)).Status); // recorrente é do dunning
    }
}
