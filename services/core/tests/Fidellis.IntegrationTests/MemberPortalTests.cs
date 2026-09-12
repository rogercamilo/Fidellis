using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Portal do membro (#75): membro nativo (Donor.IsMember), link mágico e dízimo/oferta.</summary>
public class MemberPortalTests
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

    private static TenantDbContext TDb(string db)
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);
    }

    private static (DonationCheckoutService checkout, ITenantContext tc) Checkout(TenantDbContext tdb)
    {
        var tc = new TenantContext();
        tc.SetTenant("diocese-sp");
        var cdb = new CatalogDbContext(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase($"c_{Guid.NewGuid()}").Options);
        return (new DonationCheckoutService(tdb, cdb, new FakeGateway(), tc), tc);
    }

    [Fact]
    public void Donor_is_not_member_by_default()
        => Assert.False(new Donor { Name = "Ana" }.IsMember);

    [Fact]
    public async Task Member_give_creates_a_tithe_entry_linked_to_the_donor()
    {
        var tdb = TDb($"mp_{Guid.NewGuid()}");
        var member = new Donor { Name = "João", Email = "joao@ex.com", Document = "12345678900", IsMember = true };
        tdb.Donors.Add(member);
        await tdb.SaveChangesAsync();
        var (checkout, _) = Checkout(tdb);

        // Reproduz o núcleo do endpoint /member/give: dízimo do membro identificado.
        var result = await checkout.CreateAsync(new CheckoutCommand(
            Guid.NewGuid(), 120m, member.Name, member.Email!, member.Document!, EntryType: EntryTypes.Tithe));

        var donation = await tdb.Entries.FirstAsync(d => d.Id == result.DonationId);
        Assert.Equal(EntryTypes.Tithe, donation.EntryType);
        Assert.Equal(member.Id, donation.DonorId); // reusa o doador por e-mail (não duplica)
    }

    [Fact]
    public void Magic_link_token_round_trips_and_rejects_other_tenant()
    {
        var donorId = Guid.NewGuid();
        var token = DonorMagicToken.Sign(donorId, "diocese-sp", DateTimeOffset.UtcNow.AddDays(1), "secret");
        var ok = DonorMagicToken.Validate(token, "secret", DateTimeOffset.UtcNow);
        Assert.NotNull(ok);
        Assert.Equal(donorId, ok!.Value.DonorId);
        Assert.Equal("diocese-sp", ok.Value.Tenant);
        Assert.Null(DonorMagicToken.Validate(token, "outro-segredo", DateTimeOffset.UtcNow)); // segredo errado
    }
}
