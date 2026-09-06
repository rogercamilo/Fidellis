using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Recibo de doação (prestação de contas). Número sequencial por organização/ano; um por doação.
/// </summary>
public sealed class Receipt : Entity
{
    public required string Number { get; set; }
    public required Guid OrganizationId { get; set; }
    public required Guid DonationId { get; set; }
    public required string DonorName { get; set; }
    public string? DonorDocument { get; set; }
    public required decimal Amount { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    // Cancelamento em estorno/chargeback (Onda 1 inc.1.4 / RF-FIN-022 / D12).
    public DateTimeOffset? CanceledAt { get; set; }
    public string? CancelReason { get; set; }
}
