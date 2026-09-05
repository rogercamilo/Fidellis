namespace Fidellis.SharedKernel;

/// <summary>
/// Implementação scoped de <see cref="ITenantContext"/>. Uma instância por request.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public string? TenantId { get; private set; }

    public string? SchemaName => TenantId is null ? null : ToSchemaName(TenantId);

    public bool HasTenant => TenantId is not null;

    public void SetTenant(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("tenantId não pode ser vazio.", nameof(tenantId));

        TenantId = tenantId.Trim().ToLowerInvariant();
    }

    /// <summary>Converte um slug de tenant no nome do schema Postgres (<c>t_&lt;slug&gt;</c>).</summary>
    public static string ToSchemaName(string tenantId)
    {
        var slug = new string(tenantId.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"t_{slug}";
    }
}
