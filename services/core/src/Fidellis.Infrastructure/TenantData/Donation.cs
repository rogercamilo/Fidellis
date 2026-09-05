using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Doação/dízimo/oferta recebida por uma <see cref="Organization"/> (schema do tenant).</summary>
public sealed class Donation : Entity
{
    public required Guid OrganizationId { get; set; }
    public required decimal Amount { get; set; }
    public string Method { get; set; } = "pix";
    public string Status { get; set; } = "pending";
    public string? DonorName { get; set; }
}
