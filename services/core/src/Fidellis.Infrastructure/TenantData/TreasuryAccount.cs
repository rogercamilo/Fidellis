using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Conta de tesouraria de uma <see cref="Organization"/>: conta bancária ou caixa físico. Distinta da
/// conta contábil (<see cref="Account"/>) — a tesouraria cuida da liquidez (saldo/movimentos), não do
/// razão. Reside no schema do tenant.
/// </summary>
public sealed class TreasuryAccount : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string Name { get; set; }

    /// <summary>bank | cash.</summary>
    public string Kind { get; set; } = "bank";

    public decimal OpeningBalance { get; set; }
    public bool Active { get; set; } = true;
}
