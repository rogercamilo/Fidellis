using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Credencial de serviço da integração federada (#80 / ADR-0013 dec.9): uma chave <b>por tenant e por
/// origem</b> (ex.: <c>formattio</c>) que autoriza o canal server-to-server a lançar dízimo/oferta de um
/// membro (contexto autenticado), ao contrário do checkout público anônimo (só doação). Guardamos apenas
/// o <b>hash</b> (SHA-256 hex) da chave — o valor em claro só é exibido uma vez na emissão. Reside no
/// schema do tenant.
/// </summary>
public sealed class IntegrationCredential : Entity
{
    /// <summary>Origem da integração (ex.: <c>formattio</c>). Única por tenant.</summary>
    public required string Source { get; set; }

    /// <summary>Hash SHA-256 (hex) da chave de serviço.</summary>
    public required string KeyHash { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
