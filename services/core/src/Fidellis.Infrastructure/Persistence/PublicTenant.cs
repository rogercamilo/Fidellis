using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Persistence;

/// <summary>
/// Resolve o tenant a partir do slug no path (endpoints públicos, sem JWT). Valida a existência em
/// <c>catalog.tenants</c> antes de definir o <see cref="ITenantContext"/>.
/// </summary>
public static class PublicTenant
{
    public static async Task<bool> TryResolveAsync(
        CatalogDbContext catalog, ITenantContext tenant, string slug, CancellationToken ct = default)
    {
        var normalized = (slug ?? "").Trim().ToLowerInvariant();
        if (normalized.Length == 0 || !await catalog.Tenants.AnyAsync(t => t.Slug == normalized, ct))
            return false;

        tenant.SetTenant(normalized);
        return true;
    }
}
