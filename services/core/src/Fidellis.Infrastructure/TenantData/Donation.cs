using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doação/dízimo/oferta recebida por uma <see cref="Organization"/> (schema do tenant).</summary>
public sealed class Donation : Entity
{
    public required Guid OrganizationId { get; set; }
    public required decimal Amount { get; set; }
    public string Method { get; set; } = "pix";
    public string Status { get; set; } = "pending";
    public string? DonorName { get; set; }

    // Vínculos e dados de pagamento (passo 1 — cobrança PIX via Pagar.me).
    public Guid? DonorId { get; set; }
    public Guid? CampaignId { get; set; }
    public string? PspOrderId { get; set; }
    public string? PspChargeId { get; set; }
    public string? PixQrCode { get; set; }
    public string? PixQrCodeUrl { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
}
