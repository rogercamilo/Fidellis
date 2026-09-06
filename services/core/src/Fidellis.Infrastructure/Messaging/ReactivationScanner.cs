using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Messaging;

/// <summary>
/// Enfileira mensagens de reativação para doadores inativos (com doação anterior, mas sem doar há
/// N dias). Idempotente por doador/mês via <c>DedupeKey</c>.
/// </summary>
public sealed class ReactivationScanner(TenantDbContext db, MessageOutbox outbox, IClock clock)
{
    public async Task<int> EnqueueInactiveAsync(int days, CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow.AddDays(-days);
        var period = clock.UtcNow.ToString("yyyyMM");

        var lastPaid = await db.Donations
            .Where(d => d.Status == "paid" && d.DonorId != null && d.PaidAt != null)
            .GroupBy(d => d.DonorId!.Value)
            .Select(g => new { DonorId = g.Key, Last = g.Max(x => x.PaidAt) })
            .ToListAsync(ct);

        var inactiveIds = lastPaid.Where(x => x.Last < cutoff).Select(x => x.DonorId).ToList();
        if (inactiveIds.Count == 0) return 0;

        var donors = await db.Donors
            .Where(d => inactiveIds.Contains(d.Id) && d.Email != null)
            .ToListAsync(ct);

        var enqueued = 0;
        foreach (var donor in donors)
        {
            var msg = MessageTemplates.Render(MessageTemplates.Reactivation, new MessageContext(donor.Name));
            var created = await outbox.EnqueueAsync(new EnqueueRequest(
                MessageTemplates.Reactivation, donor.Email!, msg.Subject, msg.Body,
                DonorId: donor.Id, DedupeKey: $"react:{donor.Id}:{period}"), ct);
            if (created is not null) enqueued++;
        }
        return enqueued;
    }
}
