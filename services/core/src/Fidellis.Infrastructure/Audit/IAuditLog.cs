using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.Audit;

/// <summary>Trilha de auditoria: registra ações sensíveis com o ator do request.</summary>
public interface IAuditLog
{
    Task RecordAsync(string action, string entity, string? entityId = null, string? metadata = null, CancellationToken ct = default);
}

public sealed class AuditLog(TenantDbContext db, ICurrentUser user) : IAuditLog
{
    public async Task RecordAsync(string action, string entity, string? entityId = null, string? metadata = null, CancellationToken ct = default)
    {
        db.AuditLog.Add(new AuditLogEntry
        {
            ActorUserId = user.UserId,
            Action = action,
            Entity = entity,
            EntityId = entityId,
            Metadata = metadata,
        });
        await db.SaveChangesAsync(ct);
    }
}
