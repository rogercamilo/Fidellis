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

    // Ciclo de recorrência (passo 2). Nulo em doações avulsas.
    public Guid? RecurringDonationId { get; set; }
    public DateTimeOffset? DueAt { get; set; }
    public int Attempt { get; set; }

    // Dimensões gerenciais (Onda 1). Default aplicado quando não informado (RF-FIN-143).
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }

    // Boleto (Onda 1 inc.1.3). Nulo em doações não-boleto.
    public string? BoletoLine { get; set; }
    public string? BoletoBarcode { get; set; }
    public string? BoletoUrl { get; set; }
    public DateOnly? DueDate { get; set; }
}
