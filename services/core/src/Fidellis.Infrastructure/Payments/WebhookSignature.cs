using System.Security.Cryptography;
using System.Text;

namespace Fidellis.Infrastructure.Payments;

/// <summary>
/// Validação (pura/testável) da assinatura HMAC-SHA256 do webhook do PSP sobre o corpo bruto
/// (RF-FIN-001). Comparação em tempo constante. Aceita o header no formato <c>sha256=&lt;hex&gt;</c>
/// ou apenas o hex.
/// </summary>
public static class WebhookSignature
{
    public static bool IsValid(string secret, string rawBody, string? headerValue)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrWhiteSpace(headerValue))
            return false;

        var provided = headerValue.Contains('=')
            ? headerValue[(headerValue.IndexOf('=') + 1)..]
            : headerValue;
        provided = provided.Trim();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computed = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed.ToLowerInvariant()),
            Encoding.UTF8.GetBytes(provided.ToLowerInvariant()));
    }
}
