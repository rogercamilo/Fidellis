using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Security;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Credencial de serviço da integração federada (#80 / ADR-0013 dec.9): emite/valida a chave por tenant e
/// origem que autoriza o canal server-to-server a lançar dízimo/oferta de membro. Guarda só o hash
/// (SHA-256 hex, reusa <see cref="InvitationToken"/>). Roda no schema do tenant.
/// </summary>
public sealed class IntegrationCredentialService(TenantDbContext db)
{
    /// <summary>Gera (ou rotaciona) a chave da origem e devolve o valor em claro (exibido uma única vez).</summary>
    public async Task<string> IssueAsync(string source, CancellationToken ct = default)
    {
        var normalized = Normalize(source);
        var key = InvitationToken.Generate();
        var cred = await db.IntegrationCredentials.FirstOrDefaultAsync(c => c.Source == normalized, ct);
        if (cred is null)
        {
            cred = new IntegrationCredential { Source = normalized, KeyHash = InvitationToken.Hash(key) };
            db.IntegrationCredentials.Add(cred);
        }
        else
        {
            cred.KeyHash = InvitationToken.Hash(key);
            cred.Enabled = true;
            cred.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return key;
    }

    /// <summary>Valida a chave apresentada pelo canal (origem habilitada + hash confere).</summary>
    public async Task<bool> ValidateAsync(string source, string? key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var normalized = Normalize(source);
        var cred = await db.IntegrationCredentials.FirstOrDefaultAsync(c => c.Source == normalized && c.Enabled, ct);
        return cred is not null && cred.KeyHash == InvitationToken.Hash(key);
    }

    public async Task<(bool Configured, bool Enabled)> StatusAsync(string source, CancellationToken ct = default)
    {
        var cred = await db.IntegrationCredentials.FirstOrDefaultAsync(c => c.Source == Normalize(source), ct);
        return (cred is not null, cred?.Enabled ?? false);
    }

    private static string Normalize(string source) => source.Trim().ToLowerInvariant();
}
