namespace Fidellis.Infrastructure.Catalog;

/// <summary>
/// Índice global <c>pedido do PSP → tenant</c> (schema <c>catalog</c>). Permite o receptor de
/// webhook (que não carrega nosso JWT) descobrir a qual tenant pertence um pedido e definir o schema.
/// </summary>
public sealed class PspOrder
{
    public required string ProviderOrderId { get; set; }
    public required string TenantSlug { get; set; }
    public required Guid DonationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
