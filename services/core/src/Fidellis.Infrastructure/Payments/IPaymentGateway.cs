namespace Fidellis.Infrastructure.Payments;

/// <summary>
/// Abstração do adquirente (PSP). O scaffold implementa PIX via Pagar.me; boleto/cartão
/// entram na mesma abstração no futuro. Um <c>fake</c> desta interface é usado nos testes.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Cria um pedido PIX e retorna o QR (copia-e-cola + imagem) e os ids do PSP.</summary>
    Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest request, CancellationToken ct = default);

    /// <summary>Cria um pedido boleto e retorna a linha digitável, o código de barras, a URL do PDF e o vencimento.</summary>
    Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cria um pedido cartão de crédito à vista com o <c>card_token</c> tokenizado no front (PAN nunca
    /// no core). Resposta síncrona: aprovado ou recusado (com motivo).
    /// </summary>
    Task<CardChargeResult> CreateCardOrderAsync(CreateCardOrderRequest request, CancellationToken ct = default);

    /// <summary>Consulta o status de uma cobrança no PSP (fonte de verdade na conciliação).</summary>
    Task<ChargeStatusResult> GetChargeAsync(string chargeId, CancellationToken ct = default);

    /// <summary>Cria/registra um recebedor (destino do split) para uma unidade.</summary>
    Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest request, CancellationToken ct = default);
}

public sealed record CreatePixOrderRequest(
    decimal Amount,
    string DonorName,
    string DonorEmail,
    string DonorDocument,
    int ExpiresInSeconds = 3600,
    string? RecipientId = null,
    string? Description = null);

public sealed record PixOrderResult(
    string OrderId,
    string ChargeId,
    string Status,
    string QrCode,
    string? QrCodeUrl,
    DateTimeOffset? ExpiresAt);

public sealed record CreateBoletoOrderRequest(
    decimal Amount,
    string DonorName,
    string DonorEmail,
    string DonorDocument,
    int DueInDays = 3,
    string? RecipientId = null,
    string? Description = null);

public sealed record BoletoOrderResult(
    string OrderId,
    string ChargeId,
    string Status,
    string? Line,
    string? Barcode,
    string? BoletoUrl,
    DateOnly? DueDate);

public sealed record CreateCardOrderRequest(
    decimal Amount,
    string DonorName,
    string DonorEmail,
    string DonorDocument,
    string CardToken,
    string? RecipientId = null,
    string? Description = null);

public sealed record CardChargeResult(
    string OrderId,
    string ChargeId,
    string Status,
    string? DeclineReason,
    string? Brand,
    string? Last4);

public sealed record ChargeStatusResult(
    string ChargeId,
    string Status,
    DateTimeOffset? PaidAt);

public sealed record CreateRecipientRequest(
    string Name,
    string Email,
    string Document,
    string? PixKey = null);

public sealed record CreateRecipientResult(
    string RecipientId,
    string Status);
