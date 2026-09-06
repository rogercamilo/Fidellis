using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Tipo de doador configurável (ex.: Membro, Apoiador) — RF-FIN-182. O tipo marcado como
/// <see cref="IsRecurringDefault"/> é atribuído ao doador quando ele se torna recorrente (jornada
/// apoiador→recorrente). Reside no schema do tenant.
/// </summary>
public sealed class DonorType : Entity
{
    public required string Name { get; set; }

    /// <summary>Tipo atribuído automaticamente ao doador na conversão para recorrente.</summary>
    public bool IsRecurringDefault { get; set; }

    public bool Active { get; set; } = true;
}
