using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Lançamento contábil (razão) no schema do tenant.</summary>
public sealed class AccountingEntry : Entity
{
    public required Guid TransactionId { get; set; }
    public required string Ledger { get; set; }
    public required decimal Debit { get; set; }
    public required decimal Credit { get; set; }
}
