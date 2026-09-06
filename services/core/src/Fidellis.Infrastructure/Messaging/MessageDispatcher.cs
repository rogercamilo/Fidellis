using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidellis.Infrastructure.Messaging;

/// <summary>Despacha as mensagens <c>queued</c> da outbox pelo canal correspondente.</summary>
public sealed class MessageDispatcher(
    TenantDbContext db,
    IEnumerable<IMessageSender> senders,
    IClock clock,
    ILogger<MessageDispatcher> logger)
{
    public async Task<int> DispatchQueuedAsync(int max = 50, CancellationToken ct = default)
    {
        var pending = await db.Messages
            .Where(m => m.Status == "queued")
            .OrderBy(m => m.CreatedAt)
            .Take(max)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var message in pending)
        {
            var sender = senders.FirstOrDefault(s => s.Channel == message.Channel);
            message.Attempts++;

            if (sender is null)
            {
                message.Status = "failed";
                message.Error = $"sem sender para canal '{message.Channel}'";
                continue;
            }

            var result = await sender.SendAsync(message, ct);
            message.Status = result.Status;
            message.Error = result.Error;
            if (result.Status == "sent")
            {
                message.SentAt = clock.UtcNow;
                sent++;
            }
        }

        if (pending.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Dispatch: {Sent}/{Total} enviadas.", sent, pending.Count);
        }
        return sent;
    }
}
