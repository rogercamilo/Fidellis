using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Movimento de tesouraria (entrada/saída/transferência) numa <see cref="TreasuryAccount"/>. O saldo
/// da conta é a abertura + a soma dos movimentos. Transferências internas geram duas pernas
/// (<c>transfer_out</c> + <c>transfer_in</c>) vinculadas por <see cref="CounterpartId"/>.
/// </summary>
public sealed class TreasuryMovement : Entity
{
    public required Guid AccountId { get; set; }

    /// <summary>inflow | outflow | transfer_in | transfer_out.</summary>
    public required string Kind { get; set; }

    public required decimal Amount { get; set; }
    public string? Description { get; set; }

    /// <summary>Conta contraparte numa transferência interna.</summary>
    public Guid? CounterpartId { get; set; }

    /// <summary>Origem do movimento, quando aplicável.</summary>
    public Guid? DonationId { get; set; }
    public Guid? PayableId { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
