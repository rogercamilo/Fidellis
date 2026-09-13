using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doador (dados mínimos p/ montar o pedido no PSP). CRM completo é entregável futuro.</summary>
public sealed class Donor : Entity
{
    public required string Name { get; set; }
    public string? Email { get; set; }
    public string? Document { get; set; }
    public string? Phone { get; set; }

    /// <summary>LGPD: doador optou por não receber comunicações (a régua o pula).</summary>
    public bool ContactOptOut { get; set; }

    /// <summary>LGPD: quando os dados pessoais foram anonimizados (erasure).</summary>
    public DateTimeOffset? AnonymizedAt { get; set; }

    /// <summary>
    /// Membro da comunidade (#75): marcado pelo tenant (coordenador/admin). Habilita o autoatendimento de
    /// dízimo/oferta no portal (só membro — D-06). Nativo do Fidellis; a origem federada do Formattio
    /// (D-10/#80), quando existir, também marca este campo. Não é dado sensível por si.
    /// </summary>
    public bool IsMember { get; set; }

    /// <summary>
    /// Identidade federada (#80 / ADR-0013): id estável do membro na plataforma de origem (ex.:
    /// <c>Formando.id</c> do Formattio). É o <b>vínculo autoritativo</b> — não o e-mail (que não é único
    /// na origem). Nulo em doadores nativos do Fidellis. Combinado com <see cref="Source"/> é único.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Origem do doador/membro: <c>null</c> = nativo do Fidellis; <c>formattio</c> = veio da integração
    /// federada. Regra (ADR-0013 dec.8): origem federada <b>implica membro</b> — o import marca
    /// <see cref="IsMember"/>. Guardamos só id+origem (minimização); nada de formação/vocação.
    /// </summary>
    public string? Source { get; set; }

    // Configurabilidade/CRM (Onda 1). Jornada apoiador→recorrente (RF-FIN-182).
    public Guid? DonorTypeId { get; set; }

    /// <summary>Quando o doador se tornou recorrente pela primeira vez (nulo = ainda pontual).</summary>
    public DateTimeOffset? ConvertedAt { get; set; }
}
