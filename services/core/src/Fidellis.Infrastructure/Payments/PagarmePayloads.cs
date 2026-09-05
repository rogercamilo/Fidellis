using System.Text.Json;

namespace Fidellis.Infrastructure.Payments;

/// <summary>
/// Montagem/parse dos payloads da API Core v5 do Pagar.me. Funções puras (sem HTTP) para
/// serem cobertas por testes unitários. Valores monetários trafegam em centavos (int).
/// </summary>
public static class PagarmePayloads
{
    public static int ToCents(decimal amount) => (int)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    /// <summary>Monta o corpo de <c>POST /orders</c> para um pagamento PIX (com split opcional 100% p/ a unidade).</summary>
    public static string BuildPixOrder(CreatePixOrderRequest req)
    {
        var cents = ToCents(req.Amount);

        object payment = req.RecipientId is { Length: > 0 } rid
            ? new
            {
                payment_method = "pix",
                pix = new { expires_in = req.ExpiresInSeconds },
                split = new[]
                {
                    new
                    {
                        amount = 100,
                        recipient_id = rid,
                        type = "percentage",
                        options = new { charge_processing_fee = true, liable = true, charge_remainder_fee = true },
                    },
                },
            }
            : new
            {
                payment_method = "pix",
                pix = new { expires_in = req.ExpiresInSeconds },
            };

        var order = new
        {
            items = new[]
            {
                new { amount = cents, description = req.Description ?? "Doação", quantity = 1 },
            },
            customer = new
            {
                name = req.DonorName,
                email = req.DonorEmail,
                type = "individual",
                document = req.DonorDocument,
            },
            payments = new[] { payment },
        };

        return JsonSerializer.Serialize(order);
    }

    public static PixOrderResult ParsePixOrderResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var orderId = GetString(root, "id") ?? "";
        var charge = root.TryGetProperty("charges", out var charges) && charges.ValueKind == JsonValueKind.Array && charges.GetArrayLength() > 0
            ? charges[0]
            : default;

        var chargeId = charge.ValueKind == JsonValueKind.Object ? GetString(charge, "id") ?? "" : "";
        var status = charge.ValueKind == JsonValueKind.Object ? GetString(charge, "status") ?? "pending" : "pending";

        string qrCode = "";
        string? qrCodeUrl = null;
        DateTimeOffset? expiresAt = null;
        if (charge.ValueKind == JsonValueKind.Object && charge.TryGetProperty("last_transaction", out var tx) && tx.ValueKind == JsonValueKind.Object)
        {
            qrCode = GetString(tx, "qr_code") ?? "";
            qrCodeUrl = GetString(tx, "qr_code_url");
            expiresAt = GetDate(tx, "expires_at");
        }

        return new PixOrderResult(orderId, chargeId, status, qrCode, qrCodeUrl, expiresAt);
    }

    public static ChargeStatusResult ParseChargeResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new ChargeStatusResult(
            GetString(root, "id") ?? "",
            GetString(root, "status") ?? "unknown",
            GetDate(root, "paid_at"));
    }

    public static string BuildRecipient(CreateRecipientRequest req)
    {
        // Payload mínimo p/ sandbox; onboarding/KYC completo (dados bancários) é entregável futuro.
        var recipient = new
        {
            name = req.Name,
            email = req.Email,
            document = req.Document,
            type = "individual",
            default_bank_account = req.PixKey is { Length: > 0 } key
                ? new { pix_key = key }
                : null,
        };
        return JsonSerializer.Serialize(recipient);
    }

    public static CreateRecipientResult ParseRecipientResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new CreateRecipientResult(
            GetString(root, "id") ?? "",
            GetString(root, "status") ?? "unknown");
    }

    private static string? GetString(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? GetDate(JsonElement el, string name)
        => GetString(el, name) is { } s && DateTimeOffset.TryParse(s, out var d) ? d : null;
}
