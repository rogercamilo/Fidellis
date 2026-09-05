using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Vínculo usuário↔organização (Rede→Unidade) no schema do tenant. O <see cref="UserId"/> referencia
/// a identidade global (<c>catalog.users</c>). A visibilidade cascateia para as filiais (descendentes
/// por <see cref="Organization.ParentId"/>) da organização vinculada.
/// </summary>
public sealed class OrgMember : Entity
{
    public required Guid UserId { get; set; }
    public required Guid OrganizationId { get; set; }
    public string Role { get; set; } = "member";
}
