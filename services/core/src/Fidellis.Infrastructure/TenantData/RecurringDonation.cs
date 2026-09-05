using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Doação recorrente (ex.: dízimo mensal). O scheduler gera uma <see cref="Donation"/> de ciclo a
/// cada período e aplica dunning nas falhas. Reside no schema do tenant.
/// </summary>
public sealed class RecurringDonation : Entity
{
    public required Guid OrganizationId { get; set; }
    public required Guid DonorId { get; set; }
    public required decimal Amount { get; set; }
    public string Frequency { get; set; } = "monthly";
    public int DayOfMonth { get; set; } = 1;

    /// <summary>active | paused | past_due | canceled.</summary>
    public string Status { get; set; } = "active";

    /// <summary>Quando a próxima cobrança deve ser gerada (ou a próxima tentativa de dunning).</summary>
    public DateTimeOffset NextChargeAt { get; set; }

    /// <summary>Tentativa de dunning corrente do ciclo (0 = em dia).</summary>
    public int Attempt { get; set; }

    public Guid? LastDonationId { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
}
