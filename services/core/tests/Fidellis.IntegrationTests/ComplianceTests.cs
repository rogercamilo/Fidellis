using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Passo 6 — link mágico do doador, auditoria e opt-out da régua.</summary>
public class ComplianceTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Secret = "um-segredo-bem-grande-para-hmac";

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public void MagicToken_roundtrips()
    {
        var donorId = Guid.NewGuid();
        var token = DonorMagicToken.Sign(donorId, "diocese-sp", T0.AddDays(30), Secret);
        var v = DonorMagicToken.Validate(token, Secret, T0);
        Assert.NotNull(v);
        Assert.Equal(donorId, v!.Value.DonorId);
        Assert.Equal("diocese-sp", v.Value.Tenant);
    }

    [Fact]
    public void MagicToken_rejects_wrong_secret_and_expiry()
    {
        var token = DonorMagicToken.Sign(Guid.NewGuid(), "diocese-sp", T0.AddDays(1), Secret);
        Assert.Null(DonorMagicToken.Validate(token, "outro-segredo", T0));
        Assert.Null(DonorMagicToken.Validate(token, Secret, T0.AddDays(2))); // expirado
        Assert.Null(DonorMagicToken.Validate(token + "x", Secret, T0)); // adulterado
    }

    [Fact]
    public async Task AuditLog_records_actor()
    {
        var tdb = TDb($"cmp_{Guid.NewGuid()}");
        var user = new CurrentUser();
        user.SetUser(Guid.NewGuid());
        await new AuditLog(tdb, user).RecordAsync("lgpd.export", "donor", "abc");

        var entry = await tdb.AuditLog.SingleAsync();
        Assert.Equal("lgpd.export", entry.Action);
        Assert.Equal(user.UserId, entry.ActorUserId);
    }

    [Fact]
    public async Task OptOut_donor_is_skipped_by_thank_you()
    {
        var tdb = TDb($"cmp_{Guid.NewGuid()}");
        var donor = new Donor { Name = "Ana", Email = "ana@x.org", ContactOptOut = true };
        tdb.Donors.Add(donor);
        var donation = new Donation { OrganizationId = Guid.NewGuid(), Amount = 50m, Method = "pix", Status = "paid", DonorId = donor.Id };
        tdb.Donations.Add(donation);
        await tdb.SaveChangesAsync();

        await new OutboxNotifier(tdb, new MessageOutbox(tdb)).DonationPaidAsync(donation, "2026/000001");

        Assert.Equal(0, await tdb.Messages.CountAsync());
    }

    [Fact]
    public async Task OptOut_donor_is_skipped_by_reactivation()
    {
        var tdb = TDb($"cmp_{Guid.NewGuid()}");
        var donor = new Donor { Name = "Bia", Email = "bia@x.org", ContactOptOut = true };
        tdb.Donors.Add(donor);
        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 30m, Method = "pix", Status = "paid",
            DonorId = donor.Id, PaidAt = T0.AddDays(-200),
        });
        await tdb.SaveChangesAsync();

        var enq = await new ReactivationScanner(tdb, new MessageOutbox(tdb), new FixedClock(T0)).EnqueueInactiveAsync(90);
        Assert.Equal(0, enq);
    }
}
