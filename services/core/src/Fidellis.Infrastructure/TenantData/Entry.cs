using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doação/dízimo/oferta recebida por uma <see cref="Organization"/> (schema do tenant).</summary>
public sealed class Entry : Entity
{
    public required Guid OrganizationId { get; set; }
    public required decimal Amount { get; set; }
    public string Method { get; set; } = "pix";
    public string Status { get; set; } = "pending";
    public string? DonorName { get; set; }

    /// <summary>
    /// Origem/canal da entrada (D-05): <c>checkout</c> | <c>portal</c> | <c>cash</c> | <c>manual</c>.
    /// Torna a entrada agnóstica a canal — caixa físico e lançamento manual nascem de 1ª classe, como o
    /// checkout. O instrumento (pix/boleto/cartão/espécie) segue em <see cref="Method"/>.
    /// </summary>
    public string Source { get; set; } = "checkout";

    /// <summary>
    /// Tipo da entrada de 1ª classe (D-06): <see cref="EntryTypes"/> (<c>tithe|offering|donation</c>).
    /// Substitui a inferência "recorrente ⇒ dízimo". Chave técnica estável; rótulo é por tenant.
    /// </summary>
    public string EntryType { get; set; } = EntryTypes.Donation;

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

    // Cartão (Onda 1 inc.1.4). O PAN nunca trafega/persiste — só bandeira e 4 últimos.
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }

    /// <summary>Motivo da recusa do PSP quando <c>Status = "declined"</c>.</summary>
    public string? DeclineReason { get; set; }

    /// <summary>Título a receber que esta doação quita (Onda 2 — baixa por vínculo explícito).</summary>
    public Guid? ReceivableId { get; set; }
}
