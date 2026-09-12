using System.Security.Cryptography;
using System.Text;

namespace Fidellis.Infrastructure.Security;

/// <summary>
/// Token do convite de membro (D-01). O token em claro é aleatório forte (128 bits, base64url) e
/// só viaja no link do e-mail; o banco guarda apenas o <see cref="Hash"/> (SHA-256 hex). O aceite
/// (no BFF) recomputa o hash do token recebido e o casa com a linha — sem segredo compartilhado,
/// porque o próprio token já é de alta entropia. Puro/testável.
/// </summary>
public static class InvitationToken
{
    /// <summary>Gera um token aleatório em claro (para o link do e-mail).</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Hash determinístico (SHA-256 hex minúsculo) usado como chave de busca do convite.</summary>
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();
}
