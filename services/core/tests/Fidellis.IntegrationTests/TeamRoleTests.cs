using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Atribuição de papéis financeiros (DT-04): TeamService.</summary>
public class TeamRoleTests
{
    private static CatalogDbContext Catalog(string db)
        => new(new DbContextOptionsBuilder<CatalogDbContext>().UseInMemoryDatabase(db).Options);

    private static async Task<(CatalogDbContext catalog, Guid tenantId, Guid userId)> SeededAsync(string db)
    {
        var catalog = Catalog(db);
        var tenant = Tenant.Create("diocese-sp", "Diocese SP");
        var user = new User { Email = "tesoureiro@ex.com", PasswordHash = "x", DisplayName = "Tesoureiro" };
        catalog.Tenants.Add(tenant);
        catalog.Users.Add(user);
        catalog.Memberships.Add(new Membership { UserId = user.Id, TenantId = tenant.Id, Role = "member" });
        await catalog.SaveChangesAsync();
        return (catalog, tenant.Id, user.Id);
    }

    [Fact]
    public async Task Resolve_and_list_members()
    {
        var (catalog, tenantId, _) = await SeededAsync($"team_{Guid.NewGuid()}");
        var svc = new TeamService(catalog);
        Assert.Equal(tenantId, await svc.ResolveTenantIdAsync("diocese-sp"));
        var members = await svc.ListAsync(tenantId);
        Assert.Single(members);
        Assert.Equal("member", members[0].Role);
    }

    [Fact]
    public async Task Set_role_updates_membership()
    {
        var (catalog, tenantId, userId) = await SeededAsync($"team_{Guid.NewGuid()}");
        var svc = new TeamService(catalog);
        var m = await svc.SetRoleAsync(tenantId, userId, "treasurer");
        Assert.NotNull(m);
        Assert.Equal("treasurer", (await catalog.Memberships.SingleAsync()).Role);
    }

    [Fact]
    public async Task Set_role_rejects_invalid_and_unknown()
    {
        var (catalog, tenantId, userId) = await SeededAsync($"team_{Guid.NewGuid()}");
        var svc = new TeamService(catalog);
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SetRoleAsync(tenantId, userId, "chefão"));
        Assert.Null(await svc.SetRoleAsync(tenantId, Guid.NewGuid(), "treasurer")); // usuário inexistente
    }
}
