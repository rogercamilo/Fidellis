using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Configurações financeiras do tenant (linha única). Guarda a nomenclatura própria da organização por
/// tipo de entrada (RF-FIN-180/181) — rótulos de UI/relatórios, sem alterar a mecânica. Reside no schema
/// do tenant.
/// </summary>
public sealed class FinanceSettings : Entity
{
    // Rótulos por tipo de entrada (D-06/D-07): chaves técnicas estáveis (EntryTypes), nomes customizáveis.
    /// <summary>Rótulo do tipo <c>tithe</c> (dízimo).</summary>
    public string TitheLabel { get; set; } = "Dízimo";

    /// <summary>Rótulo do tipo <c>offering</c> (oferta).</summary>
    public string OfferingLabel { get; set; } = "Oferta";

    /// <summary>Rótulo do tipo <c>donation</c> (doação).</summary>
    public string DonationLabel { get; set; } = "Doação";

    /// <summary>
    /// Modo "Gestão avançada" (D-09): quando ligado, habilita os pontos fora da curva do público-base
    /// (convênios/MROSC, projetos, e futuramente NF/faturamento). Default desligado.
    /// </summary>
    public bool AdvancedManagement { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Rótulo de exibição do tipo de entrada informado (fallback: a própria chave).</summary>
    public string LabelFor(string entryType) => entryType switch
    {
        EntryTypes.Tithe => TitheLabel,
        EntryTypes.Offering => OfferingLabel,
        EntryTypes.Donation => DonationLabel,
        _ => entryType,
    };
}
