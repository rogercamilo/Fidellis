using System.Security.Cryptography;
using System.Text;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Endurecimento (Onda 1 inc.1.2): assinatura HMAC do webhook + idempotência do checkout.</summary>
public class HardeningTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public int PixCalls { get; private set; }
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
        {
            PixCalls++;
            return Task.FromResult(new PixOrderResult($"or_{PixCalls}", $"ch_{PixCalls}", "pending", "qr", null, null));
        }
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_b", "ch_b", "pending", "34191", "barcode", "http://pdf", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))));
        public Task<CardChargeResult> CreateCardOrderAsync(CreateCardOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new CardChargeResult("or_c", "ch_c", "paid", null, "visa", "1234"));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private static TenantContext Tenant()
    {
        var t = new TenantContext(); t.SetTenant("diocese-sp"); return t;
    }

    private static TenantDbContext TDb(string db, ITenantContext tenant) =>
        new(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);

    private static CatalogDbContext CatalogInMemory() =>
        new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase($"cat_{Guid.NewGuid()}").Options);

    private static string Sign(string secret, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
    }

    [Fact]
    public void Webhook_signature_accepts_valid_and_rejects_tampered()
    {
        const string secret = "whsec_test";
        const string body = "{\"id\":\"hook_1\",\"type\":\"charge.paid\"}";
        var sig = Sign(secret, body);

        Assert.True(WebhookSignature.IsValid(secret, body, sig));
        Assert.True(WebhookSignature.IsValid(secret, body, $"sha256={sig}"));      // com prefixo
        Assert.False(WebhookSignature.IsValid(secret, body, "deadbeef"));          // assinatura errada
        Assert.False(WebhookSignature.IsValid(secret, body + " ", sig));           // corpo adulterado
        Assert.False(WebhookSignature.IsValid(secret, body, null));               // sem header
    }

    [Fact]
    public async Task Same_idempotency_key_reuses_the_donation()
    {
        var tenant = Tenant();
        var tdb = TDb($"idem_{Guid.NewGuid()}", tenant);
        var gateway = new FakeGateway();
        var checkout = new DonationCheckoutService(tdb, CatalogInMemory(), gateway, tenant);

        var cmd = new CheckoutCommand(Guid.NewGuid(), 100m, "Ana", "ana@ex.com", "12345678900",
            IdempotencyKey: "key-123");

        var first = await checkout.CreateAsync(cmd);
        var second = await checkout.CreateAsync(cmd);

        Assert.Equal(first.DonationId, second.DonationId);
        Assert.Equal(1, await tdb.Donations.CountAsync());
        Assert.Equal(1, gateway.PixCalls);   // não criou novo pedido no PSP
    }

    [Fact]
    public async Task Different_idempotency_keys_create_distinct_donations()
    {
        var tenant = Tenant();
        var tdb = TDb($"idem_{Guid.NewGuid()}", tenant);
        var checkout = new DonationCheckoutService(tdb, CatalogInMemory(), new FakeGateway(), tenant);

        var a = await checkout.CreateAsync(new CheckoutCommand(Guid.NewGuid(), 10m, "Ana", "ana@ex.com", "1", IdempotencyKey: "k1"));
        var b = await checkout.CreateAsync(new CheckoutCommand(Guid.NewGuid(), 20m, "Bia", "bia@ex.com", "2", IdempotencyKey: "k2"));

        Assert.NotEqual(a.DonationId, b.DonationId);
        Assert.Equal(2, await tdb.Donations.CountAsync());
    }
}
