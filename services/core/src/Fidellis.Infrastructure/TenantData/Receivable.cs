using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Título a receber (Contas a Receber): promessa de doação (pledge) de um doador ou parcela de
/// convênio/edital (grant/agreement). Distinto da doação já paga — é o compromisso futuro que
/// alimenta a previsibilidade e é baixado quando o recurso entra. Reside no schema do tenant.
/// </summary>
public sealed class Receivable : Entity
{
    public required Guid OrganizationId { get; set; }
    public Guid? DonorId { get; set; }

    /// <summary>pledge | grant | agreement.</summary>
    public string Source { get; set; } = "pledge";

    public string? Description { get; set; }
    public required decimal Amount { get; set; }
    public required DateOnly DueDate { get; set; }

    /// <summary>open | partial | received | canceled.</summary>
    public string Status { get; set; } = "open";

    public decimal ReceivedAmount { get; set; }

    // Dimensões gerenciais (Onda 1).
    public Guid? CostCenterId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? FundId { get; set; }

    /// <summary>Doação que quitou (baixa por vínculo explícito).</summary>
    public Guid? DonationId { get; set; }
}
