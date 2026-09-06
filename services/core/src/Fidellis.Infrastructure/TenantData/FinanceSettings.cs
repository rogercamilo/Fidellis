using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Configurações financeiras do tenant (linha única). Guarda a nomenclatura própria da organização
/// para a doação recorrente e a pontual (RF-FIN-180/181) — rótulos de UI/relatórios, sem alterar a
/// mecânica. Reside no schema do tenant.
/// </summary>
public sealed class FinanceSettings : Entity
{
    /// <summary>Rótulo da doação recorrente (ex.: Dízimo, Contribuição, Mensalidade).</summary>
    public string RecurringLabel { get; set; } = "Dízimo";

    /// <summary>Rótulo da doação pontual (ex.: Oferta, Apoio, Doação avulsa).</summary>
    public string OnetimeLabel { get; set; } = "Oferta";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
