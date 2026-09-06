using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Orçamento por dimensão e ano (RF-FIN-150): valor previsto de receita/despesa para um recorte de
/// centro de custo/projeto/fundo. Dimensões nulas = não restringe aquela dimensão. Revisões
/// versionadas (RF-FIN-152) no inc.3.4. Reside no schema do tenant.
/// </summary>
public sealed class Budget : Entity
{
    public required int Year { get; set; }
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }

    /// <summary>revenue | expense.</summary>
    public required string Kind { get; set; }
    public required decimal Amount { get; set; }

    public int Revision { get; set; } = 1;
    public bool Active { get; set; } = true;
}
