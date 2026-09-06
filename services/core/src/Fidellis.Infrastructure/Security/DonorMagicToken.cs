using System.Security.Cryptography;
using System.Text;

namespace Fidellis.Infrastructure.Security;

/// <summary>
/// Token de "link mágico" do doador (HMAC HS256, sem estado). Assina/valida
/// <c>{donorId, tenant, exp}</c> com o segredo da aplicação. Puro/testável.
/// </summary>
public static class DonorMagicToken
{
    public static string Sign(Guid donorId, string tenant, DateTimeOffset expires, string secret)
    {
        var payload = $"{donorId:N}|{tenant}|{expires.ToUnixTimeSeconds()}";
        var body = Base64Url(Encoding.UTF8.GetBytes(payload));
        return $"{body}.{Base64Url(Hmac(secret, body))}";
    }

    public static (Guid DonorId, string Tenant)? Validate(string token, string secret, DateTimeOffset now)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return null;

            var expected = Base64Url(Hmac(secret, parts[0]));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[1])))
                return null;

            var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
            var fields = payload.Split('|');
            if (fields.Length != 3) return null;

            var expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(fields[2]));
            if (expires < now) return null;

            return (Guid.ParseExact(fields[0], "N"), fields[1]);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Hmac(string secret, string data)
        => HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(data));

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
