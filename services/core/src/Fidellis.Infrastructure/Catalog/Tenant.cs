using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.Catalog;

/// <summary>Registro de uma instituição assinante no schema global <c>catalog</c>.</summary>
public sealed class Tenant : Entity
{
    public required string Slug { get; set; }
    public required string Name { get; set; }
    public required string SchemaName { get; set; }
    public string Plan { get; set; } = "trial";
    public string Status { get; set; } = "active";

    public static Tenant Create(string slug, string name)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return new Tenant
        {
            Slug = normalized,
            Name = name.Trim(),
            SchemaName = TenantContext.ToSchemaName(normalized),
        };
    }
}
