using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Evento de webhook do PSP, registrado para <b>idempotência</b> (chave única
/// <see cref="ProviderEventId"/>) e trilha de auditoria. Reside no schema do tenant.
/// </summary>
public sealed class PaymentEvent : Entity
{
    public string Provider { get; set; } = "pagarme";
    public required string ProviderEventId { get; set; }
    public required string EventType { get; set; }
    public string? ChargeId { get; set; }
    public string? Payload { get; set; }
    public string Status { get; set; } = "received";
    public DateTimeOffset? ProcessedAt { get; set; }
}
