using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Campanha/oferta de uma <see cref="Organization"/> para agrupar doações (schema do tenant).</summary>
public sealed class Campaign : Entity
{
    public required Guid OrganizationId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public decimal? GoalAmount { get; set; }
    public string Status { get; set; } = "active";
}
