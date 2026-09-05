using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.Catalog;

/// <summary>Vínculo <c>user ↔ tenant</c> com papel (RBAC), no schema global <c>catalog</c>.</summary>
public sealed class Membership : Entity
{
    public required Guid UserId { get; set; }
    public required Guid TenantId { get; set; }
    public string Role { get; set; } = "member";
}
