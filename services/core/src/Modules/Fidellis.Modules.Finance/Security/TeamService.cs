using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Security;

/// <summary>Membro do tenant e seu papel (RBAC financeiro).</summary>
public sealed record TeamMember(Guid UserId, string Email, string? DisplayName, string Role);

/// <summary>
/// Atribuição de papéis financeiros aos membros do tenant (DT-04). O papel vive na
/// <c>catalog.memberships</c> e alimenta o claim <c>role</c> do JWT — de onde o RBAC (FinanceWriteFilter)
/// e as alçadas (ApprovalService) o leem. Opera no schema global <c>catalog</c>.
/// </summary>
public sealed class TeamService(CatalogDbContext catalog)
{
    public Task<Guid> ResolveTenantIdAsync(string slug, CancellationToken ct = default)
        => catalog.Tenants.Where(t => t.Slug == slug).Select(t => t.Id).FirstOrDefaultAsync(ct);

    public Task<List<TeamMember>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => (from m in catalog.Memberships
            join u in catalog.Users on m.UserId equals u.Id
            where m.TenantId == tenantId
            orderby u.Email
            select new TeamMember(m.UserId, u.Email, u.DisplayName, m.Role))
            .ToListAsync(ct);

    /// <summary>Define o papel de um membro. Lança se o papel for inválido; retorna null se o membro não existir.</summary>
    public async Task<Membership?> SetRoleAsync(Guid tenantId, Guid userId, string role, CancellationToken ct = default)
    {
        if (!FinanceRoles.IsValid(role))
            throw new ArgumentException($"Papel inválido. Use um de: {string.Join(", ", FinanceRoles.All)}.");

        var membership = await catalog.Memberships.FirstOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
        if (membership is null) return null;
        membership.Role = role.Trim().ToLowerInvariant();
        await catalog.SaveChangesAsync(ct);
        return membership;
    }
}
