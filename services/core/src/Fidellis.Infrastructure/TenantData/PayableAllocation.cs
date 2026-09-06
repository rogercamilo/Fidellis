using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Rateio de um <see cref="Payable"/> entre dimensões (RF-FIN-115): ex.: a conta de luz da sede
/// dividida por projeto. A soma das alocações deve igualar o valor do título. Reside no schema do tenant.
/// </summary>
public sealed class PayableAllocation : Entity
{
    public required Guid PayableId { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }
    public required decimal Amount { get; set; }
}
