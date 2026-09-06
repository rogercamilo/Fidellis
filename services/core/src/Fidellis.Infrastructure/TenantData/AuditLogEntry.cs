using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Registro da trilha de auditoria (quem fez o quê) no schema do tenant.</summary>
public sealed class AuditLogEntry : Entity
{
    public Guid? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string Entity { get; set; }
    public string? EntityId { get; set; }
    public string? Metadata { get; set; }
}
