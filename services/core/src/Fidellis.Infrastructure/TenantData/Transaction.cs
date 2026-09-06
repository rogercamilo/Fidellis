using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Movimentação financeira em uma <see cref="Account"/> (schema do tenant).</summary>
public sealed class Transaction : Entity
{
    public required Guid AccountId { get; set; }
    public required decimal Amount { get; set; }
    public string Kind { get; set; } = "credit";
    public string? Description { get; set; }

    // Dimensões gerenciais (Onda 1). Default aplicado quando não informado (RF-FIN-143).
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }
}
