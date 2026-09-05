using System.Text.Json;

namespace Fidellis.Infrastructure.Payments;

/// <summary>Evento de webhook do Pagar.me já normalizado (order id, charge id, status).</summary>
public sealed record PagarmeWebhookEvent(
    string EventId,
    string Type,
    string? OrderId,
    string? ChargeId,
    string? ChargeStatus);

/// <summary>Parse (puro/testável) do corpo do webhook do Pagar.me, tolerante às variações order/charge.</summary>
public static class PagarmeWebhook
{
    public static PagarmeWebhookEvent Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var eventId = GetString(root, "id") ?? "";
        var type = GetString(root, "type") ?? "";
        var data = root.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;

        string? orderId = null, chargeId = null, status = null;

        if (data.ValueKind == JsonValueKind.Object)
        {
            if (type.StartsWith("charge", StringComparison.OrdinalIgnoreCase))
            {
                chargeId = GetString(data, "id");
                status = GetString(data, "status");
                if (data.TryGetProperty("order", out var order) && order.ValueKind == JsonValueKind.Object)
                    orderId = GetString(order, "id");
            }
            else if (type.StartsWith("order", StringComparison.OrdinalIgnoreCase))
            {
                orderId = GetString(data, "id");
                status = GetString(data, "status");
                if (data.TryGetProperty("charges", out var charges) && charges.ValueKind == JsonValueKind.Array && charges.GetArrayLength() > 0)
                {
                    chargeId = GetString(charges[0], "id");
                    status = GetString(charges[0], "status") ?? status;
                }
            }
        }

        return new PagarmeWebhookEvent(eventId, type, orderId, chargeId, status);
    }

    private static string? GetString(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
