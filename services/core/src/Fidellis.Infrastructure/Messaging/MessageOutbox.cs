using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Messaging;

public sealed record EnqueueRequest(
    string EventType,
    string ToAddress,
    string Subject,
    string Body,
    Guid? DonorId = null,
    string Channel = "email",
    string? DedupeKey = null);

/// <summary>Enfileira mensagens na outbox do tenant. Idempotente por <c>DedupeKey</c>.</summary>
public sealed class MessageOutbox(TenantDbContext db)
{
    /// <summary>Retorna a mensagem criada, ou <c>null</c> se ignorada (dedupe) ou sem destinatário.</summary>
    public async Task<OutboxMessage?> EnqueueAsync(EnqueueRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.ToAddress))
            return null;

        if (req.DedupeKey is { Length: > 0 } key &&
            await db.Messages.AnyAsync(m => m.DedupeKey == key, ct))
            return null;

        var message = new OutboxMessage
        {
            DonorId = req.DonorId,
            Channel = req.Channel,
            EventType = req.EventType,
            Template = req.EventType,
            ToAddress = req.ToAddress,
            Subject = req.Subject,
            Body = req.Body,
            Status = "queued",
            DedupeKey = req.DedupeKey,
        };
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
        return message;
    }
}
