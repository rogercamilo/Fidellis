using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Conexão federada estabelecida (#80 / P1b) — persiste, por tenant e origem, o que o puller precisa para
/// chamar o Formattio (substitui os env manuais): base URL do Formattio, id da organização de origem, o
/// segredo de pull (Bearer que o Fidellis envia) e a unidade do Fidellis à qual os membros se vinculam.
/// Criada pela troca do código de pareamento (<see cref="Security.ConnectToken"/>). Reside no schema do tenant.
/// </summary>
public sealed class IntegrationConnection : Entity
{
    /// <summary>Origem (ex.: <c>formattio</c>). Única por tenant.</summary>
    public required string Source { get; set; }

    /// <summary>Unidade (organization) do Fidellis à qual os membros federados se vinculam.</summary>
    public required Guid OrganizationId { get; set; }

    /// <summary>Base da API do Formattio (para o puller).</summary>
    public required string ExternalBaseUrl { get; set; }

    /// <summary>Id da organização na origem (Formattio <c>organizacaoId</c>).</summary>
    public required string ExternalOrgId { get; set; }

    /// <summary>Segredo de pull (Bearer) que o Fidellis envia ao Formattio. TODO: mover para cofre/cifra.</summary>
    public required string PullSecret { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset? LastSyncAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
