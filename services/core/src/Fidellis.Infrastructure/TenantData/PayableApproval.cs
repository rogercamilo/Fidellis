using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Aprovação (ou rejeição) registrada de um <see cref="Payable"/> — trilha imutável da alçada
/// (RF-FIN-112). Um mesmo aprovador não assina duas vezes o mesmo título. Reside no schema do tenant.
/// </summary>
public sealed class PayableApproval : Entity
{
    public required Guid PayableId { get; set; }
    public required Guid ApproverId { get; set; }
    public required string Role { get; set; }

    /// <summary>approved | rejected.</summary>
    public required string Decision { get; set; }
}
