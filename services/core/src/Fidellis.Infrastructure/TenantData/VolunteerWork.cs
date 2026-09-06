using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Trabalho voluntário registrado a <b>valor justo</b> (RF-FIN-162 / ITG 2002): reconhecido como se
/// houvesse desembolso — gera partida dobrada (despesa/receita de serviços voluntários). Reside no
/// schema do tenant.
/// </summary>
public sealed class VolunteerWork : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string Description { get; set; }
    public required decimal FairValue { get; set; }
    public required DateOnly PerformedOn { get; set; }

    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }

    /// <summary>Lançamento contábil gerado.</summary>
    public Guid? TransactionId { get; set; }
}
