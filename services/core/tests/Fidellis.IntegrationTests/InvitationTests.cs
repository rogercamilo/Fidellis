using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Configuration;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.Modules.Finance.Security;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Convite de membro + estado de bootstrap (D-01): InvitationService e InvitationToken.</summary>
public class InvitationTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly InfrastructureOptions Options = new()
    {
        ConnectionString = "Host=localhost;Database=x;Username=x;Password=x",
        AppBaseUrl = "http://localhost:3000",
    };

    private static CatalogDbContext Catalog(string db)
        => new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(db).Options);

    private static TenantDbContext Tenant(string db)
    {
        var tc = new TenantContext();
        tc.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tc);
    }

    private static async Task<(InvitationService svc, CatalogDbContext cat, TenantDbContext tdb, Guid tenantId)> SetupAsync(string db)
    {
        var cat = Catalog($"cat_{db}");
        var tdb = Tenant($"t_{db}");
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync(); // faixas default → papéis aprovadores
        var tenant = Fidellis.Infrastructure.Catalog.Tenant.Create("diocese-sp", "Diocese SP");
        cat.Tenants.Add(tenant);
        await cat.SaveChangesAsync();
        var svc = new InvitationService(cat, tdb, new MessageOutbox(tdb), new FixedClock(T0), Options);
        return (svc, cat, tdb, tenant.Id);
    }

    [Fact]
    public async Task Invite_new_email_creates_pending_invitation_and_email()
    {
        var (svc, cat, tdb, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        var result = await svc.CreateAsync(tenantId, "Diocese SP", "novo@ex.com", "coordinator", Guid.NewGuid());

        Assert.Equal(InviteOutcome.Invited, result.Outcome);
        Assert.NotNull(result.Invitation);
        var inv = await cat.Invitations.SingleAsync();
        Assert.Equal("pending", inv.Status);
        Assert.Equal("novo@ex.com", inv.Email);
        Assert.NotEqual("novo@ex.com", inv.TokenHash); // guarda só o hash
        Assert.Equal(T0.AddDays(7), inv.ExpiresAt);
        Assert.Single(tdb.Messages.Where(m => m.EventType == "team.invitation"));
    }

    [Fact]
    public async Task Invite_existing_user_creates_membership_directly()
    {
        var (svc, cat, tdb, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        var user = new User { Email = "ja@ex.com", PasswordHash = "x", DisplayName = "Já Existe" };
        cat.Users.Add(user);
        await cat.SaveChangesAsync();

        var result = await svc.CreateAsync(tenantId, "Diocese SP", "JA@ex.com", "council_officer", Guid.NewGuid());

        Assert.Equal(InviteOutcome.MemberAdded, result.Outcome);
        var m = await cat.Memberships.SingleAsync(x => x.UserId == user.Id && x.TenantId == tenantId);
        Assert.Equal("council_officer", m.Role);
        Assert.Empty(await cat.Invitations.ToListAsync()); // não cria convite
        Assert.Single(tdb.Messages.Where(x => x.EventType == "team.member_added"));
    }

    [Fact]
    public async Task Invite_existing_member_is_a_noop()
    {
        var (svc, cat, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        var user = new User { Email = "membro@ex.com", PasswordHash = "x" };
        cat.Users.Add(user);
        cat.Memberships.Add(new Membership { UserId = user.Id, TenantId = tenantId, Role = "coordinator" });
        await cat.SaveChangesAsync();

        var result = await svc.CreateAsync(tenantId, "Diocese SP", "membro@ex.com", "council_officer", Guid.NewGuid());
        Assert.Equal(InviteOutcome.AlreadyMember, result.Outcome);
        Assert.Equal("coordinator", (await cat.Memberships.SingleAsync()).Role); // papel inalterado
    }

    [Fact]
    public async Task Resend_regenerates_token_and_resets_expiry()
    {
        var (svc, cat, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        await svc.CreateAsync(tenantId, "Diocese SP", "novo@ex.com", "coordinator", null);
        var before = await cat.Invitations.AsNoTracking().SingleAsync();

        var clock = new FixedClock(T0.AddDays(1));
        var svc2 = new InvitationService(cat, Tenant("resend"), new MessageOutbox(Tenant("resend2")), clock, Options);
        var dto = await svc2.ResendAsync(tenantId, before.Id, "Diocese SP");

        Assert.NotNull(dto);
        var after = await cat.Invitations.AsNoTracking().SingleAsync();
        Assert.NotEqual(before.TokenHash, after.TokenHash);
        Assert.Equal(T0.AddDays(1).AddDays(7), after.ExpiresAt);
    }

    [Fact]
    public async Task Revoke_marks_invitation_revoked()
    {
        var (svc, cat, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        await svc.CreateAsync(tenantId, "Diocese SP", "novo@ex.com", "coordinator", null);
        var inv = await cat.Invitations.AsNoTracking().SingleAsync();

        Assert.True(await svc.RevokeAsync(tenantId, inv.Id));
        Assert.Equal("revoked", (await cat.Invitations.SingleAsync()).Status);
        Assert.False(await svc.RevokeAsync(tenantId, inv.Id)); // já não está pendente
    }

    [Fact]
    public async Task Bootstrap_needs_two_distinct_approvers()
    {
        var (svc, cat, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));

        var s0 = await svc.BootstrapStatusAsync(tenantId);
        Assert.Equal(0, s0.ApproverCount);
        Assert.False(s0.TeamReady);
        Assert.True(s0.InBootstrap);

        // Papéis aprovadores vêm das faixas default (coordinator, council_officer, council_chair).
        cat.Memberships.Add(new Membership { UserId = Guid.NewGuid(), TenantId = tenantId, Role = "coordinator" });
        cat.Memberships.Add(new Membership { UserId = Guid.NewGuid(), TenantId = tenantId, Role = "member" }); // não aprova
        await cat.SaveChangesAsync();
        var s1 = await svc.BootstrapStatusAsync(tenantId);
        Assert.Equal(1, s1.ApproverCount);
        Assert.False(s1.TeamReady);

        cat.Memberships.Add(new Membership { UserId = Guid.NewGuid(), TenantId = tenantId, Role = "council_officer" });
        await cat.SaveChangesAsync();
        var s2 = await svc.BootstrapStatusAsync(tenantId);
        Assert.Equal(2, s2.ApproverCount);
        Assert.True(s2.TeamReady);
        Assert.False(s2.InBootstrap);
    }

    [Fact]
    public async Task Complete_onboarding_sets_flag_once()
    {
        var (svc, cat, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        var s = await svc.CompleteOnboardingAsync(tenantId);
        Assert.NotNull(s.OnboardingCompletedAt);
        Assert.Equal(T0, (await cat.Tenants.SingleAsync()).OnboardingCompletedAt);
    }

    [Fact]
    public async Task Invalid_role_and_email_are_rejected()
    {
        var (svc, _, _, tenantId) = await SetupAsync(Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(tenantId, "Diocese SP", "ok@ex.com", "chefão", null));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateAsync(tenantId, "Diocese SP", "sem-arroba", "coordinator", null));
    }

    [Fact]
    public void Token_hash_is_deterministic_and_generate_is_unique()
    {
        var t = InvitationToken.Generate();
        Assert.Equal(InvitationToken.Hash(t), InvitationToken.Hash(t));
        Assert.NotEqual(t, InvitationToken.Hash(t));
        Assert.NotEqual(InvitationToken.Generate(), InvitationToken.Generate());
    }

    [Fact]
    public void CanInvite_covers_admin_and_coordinator_only()
    {
        Assert.True(FinanceRoles.CanInvite("admin"));
        Assert.True(FinanceRoles.CanInvite("coordinator"));
        Assert.False(FinanceRoles.CanInvite("council_officer"));
        Assert.False(FinanceRoles.CanInvite("fiscal_council"));
        Assert.False(FinanceRoles.CanInvite(null));
    }
}
