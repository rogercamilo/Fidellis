using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fidellis.Api.Auth;

/// <summary>
/// Validador HS256 auto-contido para os JWTs emitidos pelo BFF (segredo compartilhado).
/// Evita depender de pacotes de auth externos no scaffold; valida assinatura e expiração
/// e devolve as claims. Para produção/JWKS, trocar por Microsoft.AspNetCore.Authentication.JwtBearer.
/// </summary>
public static class JwtTokenReader
{
    /// <summary>Valida o token e retorna as claims, ou <c>null</c> se inválido/expirado.</summary>
    public static Dictionary<string, JsonElement>? TryValidate(string token, string secret)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var signingInput = $"{parts[0]}.{parts[1]}";
            var expected = Base64UrlEncode(
                HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput)));

            // comparação em tempo constante
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2])))
                return null;

            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
            if (claims is null) return null;

            if (claims.TryGetValue("exp", out var exp) && exp.TryGetInt64(out var expUnix))
            {
                if (DateTimeOffset.FromUnixTimeSeconds(expUnix) < DateTimeOffset.UtcNow)
                    return null;
            }

            return claims;
        }
        catch
        {
            return null;
        }
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string input)
    {
        var s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
