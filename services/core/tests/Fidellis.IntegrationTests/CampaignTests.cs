using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Campanhas (D-08): progresso (arrecadado × meta), earmark de fundo, janela e prestação de contas.</summary>
public class CampaignTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
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

    private static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);
    }

    private static CatalogDbContext CDb(string db)
        => new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(db).Options);

    [Fact]
    public async Task Progress_sums_paid_donations_against_goal()
    {
        var tdb = TDb($"cp_{Guid.NewGuid()}");
        var svc = new CampaignService(tdb, new FixedClock(T0));
        var org = Guid.NewGuid();
        var c = await svc.CreateAsync(org, "Reforma do telhado", null, 1000m, "Ajude", null, null, null, null);

        tdb.Donations.Add(new Donation { OrganizationId = org, Amount = 300m, Status = "paid", CampaignId = c.Id });
        tdb.Donations.Add(new Donation { OrganizationId = org, Amount = 100m, Status = "pending", CampaignId = c.Id }); // não conta
        await tdb.SaveChangesAsync();

        var p = await svc.ProgressAsync(c);
        Assert.Equal(300m, p.Raised);
        Assert.Equal(30.0m, p.Percent);
        Assert.True(p.Active);
        Assert.Equal("reforma-do-telhado", c.Slug);
    }

    [Fact]
    public async Task Campaign_donation_inherits_restricted_fund()
    {
        var tdb = TDb($"cp_{Guid.NewGuid()}");
        var cdb = CDb($"cc_{Guid.NewGuid()}");
        var org = Guid.NewGuid();
        var fund = new Fund { Code = "OBR", Name = "Obras", Restriction = "restricted", Purpose = "Reforma" };
        tdb.Funds.Add(fund);
        await tdb.SaveChangesAsync();

        var campaigns = new CampaignService(tdb, new FixedClock(T0));
        var campaign = await campaigns.CreateAsync(org, "Reforma", null, 1000m, null, null, null, fund.Id, null);

        var tc = new TenantContext();
        tc.SetTenant("diocese-sp");
        var checkout = new DonationCheckoutService(tdb, cdb, new FakeGateway(), tc);
        var result = await checkout.CreateAsync(new CheckoutCommand(
            org, 250m, "Doador", "d@x.org", "123", CampaignId: campaign.Id, EntryType: EntryTypes.Donation));

        var donation = await tdb.Donations.FirstAsync(d => d.Id == result.DonationId);
        Assert.Equal(fund.Id, donation.FundId); // herdou o fundo restrito da campanha (earmark)
    }

    [Fact]
    public async Task Window_controls_active_flag()
    {
        var tdb = TDb($"cp_{Guid.NewGuid()}");
        var svc = new CampaignService(tdb, new FixedClock(T0)); // T0 = 2026-06-01
        var org = Guid.NewGuid();

        var future = await svc.CreateAsync(org, "Futura", null, null, null, T0.AddDays(10), T0.AddDays(20), null, null);
        var past = await svc.CreateAsync(org, "Passada", null, null, null, T0.AddDays(-20), T0.AddDays(-10), null, null);
        var open = await svc.CreateAsync(org, "Aberta", null, null, null, null, null, null, null);

        Assert.False((await svc.ProgressAsync(future)).Active);
        Assert.False((await svc.ProgressAsync(past)).Active);
        Assert.True((await svc.ProgressAsync(open)).Active);
    }

    [Fact]
    public async Task Report_computes_raised_applied_and_balance()
    {
        var tdb = TDb($"cp_{Guid.NewGuid()}");
        var svc = new CampaignService(tdb, new FixedClock(T0));
        var org = Guid.NewGuid();
        var fund = new Fund { Code = "OBR", Name = "Obras", Restriction = "restricted", Purpose = "Reforma" };
        tdb.Funds.Add(fund);
        await tdb.SaveChangesAsync();
        var c = await svc.CreateAsync(org, "Reforma", null, 1000m, null, null, null, fund.Id, null);

        tdb.Donations.Add(new Donation { OrganizationId = org, Amount = 600m, Status = "paid", CampaignId = c.Id });
        // Despesa (débito) no fundo restrito = aplicado.
        tdb.Transactions.Add(new Transaction { AccountId = Guid.NewGuid(), Amount = 420m, Kind = "debit", FundId = fund.Id, AccountingDate = new DateOnly(2026, 6, 10) });
        await tdb.SaveChangesAsync();

        var report = await svc.ReportAsync(c.Id);
        Assert.NotNull(report);
        Assert.Equal(600m, report!.Raised);
        Assert.Equal(420m, report.Applied);
        Assert.Equal(180m, report.Balance);
    }

    [Fact]
    public async Task Duplicate_slug_is_rejected()
    {
        var tdb = TDb($"cp_{Guid.NewGuid()}");
        var svc = new CampaignService(tdb, new FixedClock(T0));
        var org = Guid.NewGuid();
        await svc.CreateAsync(org, "Reforma", null, null, null, null, null, null, null);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(org, "Reforma", null, null, null, null, null, null, null));
    }
}
