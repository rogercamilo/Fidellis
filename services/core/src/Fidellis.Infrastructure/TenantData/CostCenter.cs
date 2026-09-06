using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Centro de custo (dimensão gerencial) do tenant. Toda transação/doação referencia um centro de
/// custo; quando não informado, aplica-se o marcado como <see cref="IsDefault"/>. Reside no schema
/// do tenant (<c>t_&lt;slug&gt;</c>).
/// </summary>
public sealed class CostCenter : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>Centro de custo padrão aplicado a lançamentos sem dimensão informada (RF-FIN-143).</summary>
    public bool IsDefault { get; set; }

    public bool Active { get; set; } = true;
}
