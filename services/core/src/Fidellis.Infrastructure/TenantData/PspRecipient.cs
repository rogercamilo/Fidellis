using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Recebedor do PSP (Pagar.me) vinculado a uma <see cref="Organization"/> — destino do PIX
/// (split 100% para a unidade). Reside no schema do tenant.
/// </summary>
public sealed class PspRecipient : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string ProviderRecipientId { get; set; }
    public string Status { get; set; } = "active";
}
