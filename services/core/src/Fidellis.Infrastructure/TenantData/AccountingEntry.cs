using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Lançamento contábil (razão) no schema do tenant.</summary>
public sealed class AccountingEntry : Entity
{
    public required Guid TransactionId { get; set; }

    /// <summary>Conta do plano de contas (<see cref="LedgerAccount"/>). Nulo em lançamentos legados.</summary>
    public Guid? LedgerAccountId { get; set; }

    /// <summary>Rótulo denormalizado (nome/código da conta) para exibição e compatibilidade.</summary>
    public required string Ledger { get; set; }

    public required decimal Debit { get; set; }
    public required decimal Credit { get; set; }

    /// <summary>Data de competência contábil (DT-07): a base de agregação por ano/trimestre das
    /// demonstrações, independente de <see cref="Entity.CreatedAt"/> (quando o registro foi gravado).</summary>
    public DateOnly AccountingDate { get; set; }
}
