using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Rubrica de receita/despesa configurável (RF-FIN-183), mapeável a uma conta do plano de contas
/// (<see cref="LedgerAccountId"/>). Reside no schema do tenant.
/// </summary>
public sealed class FinanceCategory : Entity
{
    /// <summary>revenue | expense.</summary>
    public required string Kind { get; set; }
    public required string Name { get; set; }

    /// <summary>Conta do plano de contas vinculada (opcional).</summary>
    public Guid? LedgerAccountId { get; set; }

    public bool Active { get; set; } = true;
}
