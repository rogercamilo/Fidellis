using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Conta do plano de contas (chart of accounts) do tenant. Os lançamentos
/// (<see cref="AccountingEntry"/>) referenciam contas <c>postable</c> (folhas).
/// </summary>
public sealed class LedgerAccount : Entity
{
    public required string Code { get; set; }
    public required string Name { get; set; }

    /// <summary>asset | liability | equity | revenue | expense.</summary>
    public required string Type { get; set; }

    /// <summary>debit | credit — lado normal do saldo.</summary>
    public required string NormalBalance { get; set; }

    /// <summary>Se aceita lançamentos (contas de grupo/sintéticas são não-postáveis).</summary>
    public bool Postable { get; set; } = true;

    public Guid? ParentId { get; set; }
}
