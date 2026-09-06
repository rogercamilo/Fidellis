using System.Text;
using System.Text.Json;
using Fidellis.Infrastructure.TenantData;
using Microsoft.Extensions.Logging;

namespace Fidellis.Infrastructure.Messaging;

/// <summary>
/// Envio de e-mail via API HTTP do Resend. Sem <c>RESEND_API_KEY</c> configurada, retorna
/// <c>skipped</c> (dev-safe) e apenas registra em log.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient http,
    InfrastructureOptions options,
    ILogger<ResendEmailSender> logger) : IMessageSender
{
    public string Channel => "email";

    /// <summary>Monta o corpo do <c>POST /emails</c> do Resend (puro/testável).</summary>
    public static string BuildPayload(string from, string to, string subject, string text)
        => JsonSerializer.Serialize(new { from, to = new[] { to }, subject, text });

    public async Task<SendResult> SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.ResendApiKey))
        {
            logger.LogInformation("[email:skipped] sem RESEND_API_KEY — para {To}: {Subject}", message.ToAddress, message.Subject);
            return SendResult.Skipped("RESEND_API_KEY ausente");
        }

        var payload = BuildPayload(options.MailFrom, message.ToAddress, message.Subject ?? "Fidellis", message.Body);
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        try
        {
            using var res = await http.PostAsync("emails", content, ct);
            if ((int)res.StatusCode is >= 200 and < 300)
                return SendResult.Sent;

            var body = await res.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Resend falhou ({Status}): {Body}", (int)res.StatusCode, body);
            return SendResult.Failed($"resend {(int)res.StatusCode}");
        }
        catch (Exception ex)
        {
            return SendResult.Failed(ex.Message);
        }
    }
}
