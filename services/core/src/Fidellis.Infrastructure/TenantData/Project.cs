using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Projeto (dimensão gerencial) do tenant, com orçamento e prazo próprios. Pode estar vinculado a um
/// <see cref="Fund"/> restrito (recurso de edital/convênio). Dimensão opcional nos lançamentos.
/// Reside no schema do tenant.
/// </summary>
public sealed class Project : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>Fundo (opcional) ao qual o projeto está vinculado.</summary>
    public Guid? FundId { get; set; }

    public decimal? BudgetAmount { get; set; }
    public DateOnly? StartsAt { get; set; }
    public DateOnly? EndsAt { get; set; }

    /// <summary>active | closed.</summary>
    public string Status { get; set; } = "active";
}
