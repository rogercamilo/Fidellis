namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Índice de idempotência de criação de cobrança (RF-FIN-003): mapeia a chave enviada pelo cliente
/// (<c>Idempotency-Key</c>) para a doação criada, evitando cobranças duplicadas em retries. Expira
/// após a janela configurada. Reside no schema do tenant.
/// </summary>
public sealed class IdempotencyKey
{
    public required string Key { get; set; }
    public required Guid DonationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}
