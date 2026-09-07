using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Régua de cobrança de Contas a Receber (RF-FIN-102, DT-06): varre os títulos em aberto/parciais com
/// doador identificado e enfileira lembretes na outbox — "a vencer" (janela de antecedência) e
/// "vencido". Idempotente por <c>DedupeKey</c>: 1 lembrete por título a vencer; no máximo 1 por mês
/// para os vencidos. Roda no schema do tenant resolvido (chamado pelo <see cref="BillingWorker"/>).
/// </summary>
public sealed class ReceivablesReminderService(TenantDbContext db, MessageOutbox outbox, IClock clock)
{
    /// <summary>Dias de antecedência para o lembrete "a vencer".</summary>
    public const int DueSoonWindowDays = 3;

    /// <summary>Enfileira os lembretes devidos. Retorna quantas mensagens foram criadas (fora dedupe).</summary>
    public async Task<int> EnqueueRemindersAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var open = await db.Receivables
            .Where(r => (r.Status == "open" || r.Status == "partial") && r.DonorId != null)
            .ToListAsync(ct);

        var enqueued = 0;
        foreach (var r in open)
        {
            var outstanding = r.Amount - r.ReceivedAmount;
            if (outstanding <= 0) continue;

            var days = r.DueDate.DayNumber - today.DayNumber;
            string eventType, dedupeKey;
            if (days < 0)
            {
                eventType = MessageTemplates.ReceivableOverdue;
                dedupeKey = $"ar-overdue:{r.Id}:{today:yyyy-MM}"; // no máx. 1 lembrete/mês por título vencido
            }
            else if (days <= DueSoonWindowDays)
            {
                eventType = MessageTemplates.ReceivableDueSoon;
                dedupeKey = $"ar-duesoon:{r.Id}"; // 1 único lembrete de "a vencer"
            }
            else continue;

            var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == r.DonorId, ct);
            if (donor is null || donor.ContactOptOut) continue;
            if (donor.Email is not { Length: > 0 } email) continue;

            var orgName = await db.Organizations
                .Where(o => o.Id == r.OrganizationId).Select(o => (string?)o.Name).FirstOrDefaultAsync(ct);
            var msg = MessageTemplates.Render(eventType,
                new MessageContext(donor.Name, orgName, outstanding, DueDate: r.DueDate));

            var created = await outbox.EnqueueAsync(new EnqueueRequest(
                eventType, email, msg.Subject, msg.Body, DonorId: donor.Id, DedupeKey: dedupeKey), ct);
            if (created is not null) enqueued++;
        }
        return enqueued;
    }
}
