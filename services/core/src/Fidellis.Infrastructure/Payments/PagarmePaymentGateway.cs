using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Fidellis.Infrastructure.Payments;

/// <summary>
/// Implementação PIX do <see cref="IPaymentGateway"/> sobre a API Core v5 do Pagar.me.
/// A montagem/parse dos payloads fica em <see cref="PagarmePayloads"/> (testável sem HTTP).
/// </summary>
public sealed class PagarmePaymentGateway(HttpClient http, ILogger<PagarmePaymentGateway> logger) : IPaymentGateway
{
    public async Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest request, CancellationToken ct = default)
    {
        var body = PagarmePayloads.BuildPixOrder(request);
        var json = await PostAsync("orders", body, ct);
        return PagarmePayloads.ParsePixOrderResponse(json);
    }

    public async Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest request, CancellationToken ct = default)
    {
        var body = PagarmePayloads.BuildBoletoOrder(request);
        var json = await PostAsync("orders", body, ct);
        return PagarmePayloads.ParseBoletoOrderResponse(json);
    }

    public async Task<ChargeStatusResult> GetChargeAsync(string chargeId, CancellationToken ct = default)
    {
        using var res = await http.GetAsync($"charges/{chargeId}", ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res.StatusCode, json, "GET /charges");
        return PagarmePayloads.ParseChargeResponse(json);
    }

    public async Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest request, CancellationToken ct = default)
    {
        var body = PagarmePayloads.BuildRecipient(request);
        var json = await PostAsync("recipients", body, ct);
        return PagarmePayloads.ParseRecipientResponse(json);
    }

    private async Task<string> PostAsync(string path, string jsonBody, CancellationToken ct)
    {
        using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        using var res = await http.PostAsync(path, content, ct);
        var json = await res.Content.ReadAsStringAsync(ct);
        EnsureSuccess(res.StatusCode, json, $"POST {path}");
        return json;
    }

    private void EnsureSuccess(System.Net.HttpStatusCode status, string json, string op)
    {
        if ((int)status is >= 200 and < 300) return;
        logger.LogError("Pagar.me {Op} falhou ({Status}): {Body}", op, (int)status, json);
        throw new PaymentGatewayException($"Pagar.me {op} retornou {(int)status}.");
    }
}

/// <summary>Erro do adquirente (resposta não-2xx ou falha de comunicação).</summary>
public sealed class PaymentGatewayException(string message) : Exception(message);
