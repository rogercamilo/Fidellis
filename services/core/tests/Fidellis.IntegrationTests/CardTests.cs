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

/// <summary>Cartão (Onda 1 inc.1.4): checkout síncrono aprovado/recusado + estorno/chargeback.</summary>
public class CardTests
{
    private sealed class CardGateway(string status, string? decline = null) : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_p", "ch_p", "pending", "qr", null, null));
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_b", "ch_b", "pending", "l", "b", "u", null));
        public Task<CardChargeResult> CreateCardOrderAsync(CreateCardOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new CardChargeResult("or_1", "ch_1", status, decline, "visa", "4242"));
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

    private static ReconciliationService Recon(TenantDbContext tdb, IClock clock) =>
        new(tdb, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock);

    [Fact]
    public async Task Approved_card_is_paid_and_reconciled_immediately()
    {
        var tenant = Tenant();
        var tdb = TDb($"card_{Guid.NewGuid()}", tenant);
        var clock = new FixedClock(T0);
        var checkout = new DonationCheckoutService(tdb, Catalog(), new CardGateway("paid"), tenant, Recon(tdb, clock));

        var result = await checkout.CreateAsync(new CheckoutCommand(
            Guid.NewGuid(), 150m, "Ana", "ana@ex.com", "12345678900", Method: "card", CardToken: "tok_visa"));

        Assert.Equal("card", result.Method);
        Assert.Equal("paid", result.Status);

        var d = await tdb.Donations.SingleAsync();
        Assert.Equal("paid", d.Status);
        Assert.Equal("4242", d.CardLast4);
        Assert.NotNull(d.PaidAt);
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync()); // conciliado na hora
        Assert.Equal(1, await tdb.Receipts.CountAsync());
    }

    [Fact]
    public async Task Declined_card_is_marked_declined_without_reconciliation()
    {
        var tenant = Tenant();
        var tdb = TDb($"card_{Guid.NewGuid()}", tenant);
        var clock = new FixedClock(T0);
        var checkout = new DonationCheckoutService(tdb, Catalog(), new CardGateway("failed", "Saldo insuficiente"), tenant, Recon(tdb, clock));

        var result = await checkout.CreateAsync(new CheckoutCommand(
            Guid.NewGuid(), 150m, "Ana", "ana@ex.com", "12345678900", Method: "card", CardToken: "tok_visa"));

        Assert.Equal("declined", result.Status);
        Assert.Equal("Saldo insuficiente", result.DeclineReason);
        Assert.Equal(0, await tdb.AccountingEntries.CountAsync());
        Assert.Equal(0, await tdb.Receipts.CountAsync());
    }

    [Fact]
    public async Task Refund_webhook_reverses_and_cancels_receipt()
    {
        var tenant = Tenant();
        var tdb = TDb($"card_{Guid.NewGuid()}", tenant);
        var clock = new FixedClock(T0);
        var processor = new WebhookProcessor(tdb, new CardGateway("paid"), new ChartOfAccountsSeeder(tdb),
            new ReceiptService(tdb, clock), new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock,
            NullLogger<WebhookProcessor>.Instance);

        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 100m, Method = "card", Status = "pending",
            PspChargeId = "ch_1", DonorName = "Ana",
        });
        await tdb.SaveChangesAsync();

        await processor.ProcessAsync(new PagarmeWebhookEvent("h1", "charge.paid", "or_1", "ch_1", "paid"), "{}");
        Assert.Equal("paid", (await tdb.Donations.SingleAsync()).Status);
        Assert.Null((await tdb.Receipts.SingleAsync()).CanceledAt);

        await processor.ProcessAsync(new PagarmeWebhookEvent("h2", "charge.refunded", "or_1", "ch_1", "refunded"), "{}");

        var d = await tdb.Donations.SingleAsync();
        Assert.Equal("refunded", d.Status);
        Assert.Equal(4, await tdb.AccountingEntries.CountAsync()); // 2 da conciliação + 2 da reversão
        var receipt = await tdb.Receipts.SingleAsync();
        Assert.NotNull(receipt.CanceledAt);
        Assert.Equal("estorno", receipt.CancelReason);
    }
}
