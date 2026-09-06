using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Período contábil (mês/ano) — governança (RF-FIN-170). Quando <c>closed</c>, bloqueia lançamentos
/// retroativos naquele mês. A reabertura exige papel <c>admin</c> e é registrada em auditoria.
/// Reside no schema do tenant.
/// </summary>
public sealed class AccountingPeriod : Entity
{
    public required int Year { get; set; }
    public required int Month { get; set; }

    /// <summary>open | closed.</summary>
    public string Status { get; set; } = "open";

    public Guid? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
