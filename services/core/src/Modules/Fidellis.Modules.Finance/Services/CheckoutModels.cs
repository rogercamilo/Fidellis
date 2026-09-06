namespace Fidellis.Modules.Finance.Services;

/// <summary>Comando de criação de cobrança PIX (gerado pelo gestor autenticado).</summary>
public sealed record CheckoutCommand(
    Guid OrganizationId,
    decimal Amount,
    string DonorName,
    string DonorEmail,
    string DonorDocument,
    Guid? CampaignId = null,
    string? Description = null,
    string? IdempotencyKey = null,
    string Method = "pix",
    string? CardToken = null);

/// <summary>Resultado do checkout: dados do PIX, do boleto ou do cartão para exibir ao doador.</summary>
public sealed record CheckoutResult(
    Guid DonationId,
    string Status,
    string Method,
    string? QrCode = null,
    string? QrCodeUrl = null,
    DateTimeOffset? ExpiresAt = null,
    string? BoletoLine = null,
    string? BoletoUrl = null,
    DateOnly? DueDate = null,
    string? DeclineReason = null);

public sealed record RecipientResult(Guid Id, string ProviderRecipientId, string Status);
