using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Notifications;

/// <summary>
/// Implementação de <see cref="INotifier"/> que enfileira mensagens da régua na outbox (passo 4).
/// Substitui o <c>LogNotifier</c>. O envio real é feito pelo <c>MessageDispatcher</c> no worker.
/// </summary>
public sealed class OutboxNotifier(TenantDbContext db, MessageOutbox outbox) : INotifier
{
    public Task ChargeCreatedAsync(RecurringDonation recurring, Donation cycle, CancellationToken ct = default)
        => Task.CompletedTask; // cobrança gerada não é um toque da régua neste passo

    public Task PaymentFailedAsync(RecurringDonation recurring, int attempt, CancellationToken ct = default)
        => EnqueueForRecurringAsync(recurring, MessageTemplates.PaymentFailed, $"dunning:{recurring.Id}:{attempt}", ct);

    public Task PastDueAsync(RecurringDonation recurring, CancellationToken ct = default)
        => EnqueueForRecurringAsync(recurring, MessageTemplates.PastDue, $"pastdue:{recurring.Id}", ct);

    public async Task DonationPaidAsync(Donation donation, string? receiptNumber, CancellationToken ct = default)
    {
        if (donation.DonorId is not { } donorId) return;
        var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == donorId, ct);
        if (donor is null || donor.ContactOptOut) return;
        if (donor.Email is not { Length: > 0 } email) return;

        var orgName = await OrgNameAsync(donation.OrganizationId, ct);
        var msg = MessageTemplates.Render(MessageTemplates.ThankYou,
            new MessageContext(donor.Name, orgName, donation.Amount, receiptNumber));
        await outbox.EnqueueAsync(new EnqueueRequest(
            MessageTemplates.ThankYou, email, msg.Subject, msg.Body,
            DonorId: donor.Id, DedupeKey: $"thankyou:{donation.Id}"), ct);
    }

    private async Task EnqueueForRecurringAsync(RecurringDonation recurring, string eventType, string dedupeKey, CancellationToken ct)
    {
        var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == recurring.DonorId, ct);
        if (donor is null || donor.ContactOptOut) return;
        if (donor.Email is not { Length: > 0 } email) return;

        var orgName = await OrgNameAsync(recurring.OrganizationId, ct);
        var msg = MessageTemplates.Render(eventType, new MessageContext(donor.Name, orgName, recurring.Amount));
        await outbox.EnqueueAsync(new EnqueueRequest(
            eventType, email, msg.Subject, msg.Body, DonorId: donor.Id, DedupeKey: dedupeKey), ct);
    }

    private Task<string?> OrgNameAsync(Guid organizationId, CancellationToken ct)
        => db.Organizations.Where(o => o.Id == organizationId).Select(o => (string?)o.Name).FirstOrDefaultAsync(ct);
}
