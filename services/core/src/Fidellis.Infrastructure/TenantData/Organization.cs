using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Unidade interna do tenant (hierarquia Rede→Unidade, ex.: paróquias de uma diocese).
/// Reside no schema do tenant (<c>t_&lt;slug&gt;</c>).
/// </summary>
public sealed class Organization : Entity
{
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }
}
