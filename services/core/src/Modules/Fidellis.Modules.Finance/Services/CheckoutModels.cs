namespace Fidellis.Modules.Finance.Services;

/// <summary>Comando de criação de cobrança PIX (gerado pelo gestor autenticado).</summary>
public sealed record CheckoutCommand(
    Guid OrganizationId,
    decimal Amount,
    string DonorName,
    string DonorEmail,
    string DonorDocument,
    Guid? CampaignId = null,
    string? Description = null);

/// <summary>Resultado do checkout: dados do PIX para exibir ao doador.</summary>
public sealed record CheckoutResult(
    Guid DonationId,
    string Status,
    string QrCode,
    string? QrCodeUrl,
    DateTimeOffset? ExpiresAt);

public sealed record RecipientResult(Guid Id, string ProviderRecipientId, string Status);
