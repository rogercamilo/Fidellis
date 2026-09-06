using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Título a pagar (Contas a Pagar): despesa/obrigação de um <see cref="Payee"/>, com vencimento,
/// rubrica, dimensões e documento fiscal. Nasce <c>awaiting_approval</c> e só é pago após passar pela
/// alçada (Onda 2 inc.2.3). Reside no schema do tenant.
/// </summary>
public sealed class Payable : Entity
{
    public required Guid PayeeId { get; set; }
    public Guid? CategoryId { get; set; }
    public required string Description { get; set; }
    public required decimal Amount { get; set; }
    public required DateOnly DueDate { get; set; }

    /// <summary>awaiting_approval | approved | scheduled | paid | rejected | canceled.</summary>
    public string Status { get; set; } = "awaiting_approval";

    public string? DocumentUrl { get; set; }

    // Dimensões gerenciais (Onda 1). Rateio detalhado em PayableAllocation.
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>Conta de tesouraria que pagou (Onda 2 inc.2.3).</summary>
    public Guid? AccountId { get; set; }

    public Guid? CreatedBy { get; set; }
}
