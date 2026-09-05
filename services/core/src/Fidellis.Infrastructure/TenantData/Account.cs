using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Conta de arrecadação/financeira de uma <see cref="Organization"/> (schema do tenant).</summary>
public sealed class Account : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string Name { get; set; }
    public string Currency { get; set; } = "BRL";
}
