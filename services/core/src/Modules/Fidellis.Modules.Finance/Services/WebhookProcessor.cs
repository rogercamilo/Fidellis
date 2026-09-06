using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Processa eventos de webhook do PSP de forma <b>idempotente</b> (dedupe por
/// <c>provider_event_id</c>). Ao confirmar o pagamento, reconsulta o PSP (fonte de verdade),
/// marca a doação como paga e faz a conciliação: cria a <c>transaction</c>, os lançamentos de
/// partida dobrada (contra o plano de contas) e emite o recibo. Roda com o <c>ITenantContext</c>
/// já resolvido.
/// </summary>
public sealed class WebhookProcessor(
    TenantDbContext db,
    IPaymentGateway gateway,
    ChartOfAccountsSeeder chartSeeder,
    ReceiptService receipts,
    INotifier notifier,
    IClock clock,
    ILogger<WebhookProcessor> logger)
{

    /// <summary>Retorna <c>true</c> se processou; <c>false</c> se foi ignorado por duplicidade.</summary>
    public async Task<bool> ProcessAsync(PagarmeWebhookEvent evt, string rawPayload, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(evt.EventId))
            return false;

        // Idempotência: se o evento já foi registrado, no-op.
        if (await db.PaymentEvents.AnyAsync(e => e.ProviderEventId == evt.EventId, ct))
        {
            logger.LogInformation("Webhook {EventId} já processado; ignorando.", evt.EventId);
            return false;
        }

        var record = new PaymentEvent
        {
            ProviderEventId = evt.EventId,
            EventType = evt.Type,
            ChargeId = evt.ChargeId,
            Payload = rawPayload,
            Status = "received",
        };
        db.PaymentEvents.Add(record);

        if (IsPaidEvent(evt.Type) && evt.ChargeId is { Length: > 0 } chargeId)
            await ConfirmPaymentAsync(chargeId, ct);
        else if (IsFailedEvent(evt.Type) && evt.ChargeId is { Length: > 0 } failedChargeId)
            await MarkFailedAsync(failedChargeId, ct);
        else if (IsRefundEvent(evt.Type) && evt.ChargeId is { Length: > 0 } refundedChargeId)
            await MarkReversedAsync(refundedChargeId, "refunded", "estorno", ct);
        else if (IsChargebackEvent(evt.Type) && evt.ChargeId is { Length: > 0 } cbChargeId)
            await MarkReversedAsync(cbChargeId, "charged_back", "chargeback", ct);

        record.Status = "processed";
        record.ProcessedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ConfirmPaymentAsync(string chargeId, CancellationToken ct)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(d => d.PspChargeId == chargeId, ct);
        if (donation is null)
        {
            logger.LogWarning("Nenhuma doação para a cobrança {ChargeId}.", chargeId);
            return;
        }
        if (donation.Status == "paid")
            return; // já conciliada

        // Fonte de verdade: reconsulta o PSP antes de confirmar.
        var charge = await gateway.GetChargeAsync(chargeId, ct);
        if (!string.Equals(charge.Status, "paid", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Cobrança {ChargeId} ainda não paga no PSP ({Status}).", chargeId, charge.Status);
            return;
        }

        donation.Status = "paid";
        donation.PaidAt = charge.PaidAt ?? DateTimeOffset.UtcNow;

        await Reconciliation().PostPaidAsync(donation, ct);
        logger.LogInformation("Doação {DonationId} conciliada (R$ {Amount}).", donation.Id, donation.Amount);
    }

    private async Task MarkFailedAsync(string chargeId, CancellationToken ct)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(d => d.PspChargeId == chargeId, ct);
        if (donation is not null && donation.Status != "paid")
            donation.Status = "failed";
    }

    /// <summary>Estorno/chargeback (RF-FIN-022): reverte a conciliação e cancela o recibo.</summary>
    private async Task MarkReversedAsync(string chargeId, string newStatus, string reason, CancellationToken ct)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(d => d.PspChargeId == chargeId, ct);
        if (donation is null)
        {
            logger.LogWarning("Estorno sem doação para a cobrança {ChargeId}.", chargeId);
            return;
        }
        await Reconciliation().ReverseAsync(donation, newStatus, reason, ct);
        logger.LogInformation("Doação {DonationId} revertida ({Status}).", donation.Id, newStatus);
    }

    private ReconciliationService Reconciliation() => new(db, chartSeeder, receipts, notifier, clock);

    private static bool IsPaidEvent(string type)
        => type is "charge.paid" or "order.paid";

    private static bool IsFailedEvent(string type)
        => type is "charge.payment_failed" or "order.payment_failed";

    private static bool IsRefundEvent(string type)
        => type is "charge.refunded" or "order.refunded";

    private static bool IsChargebackEvent(string type)
        => type is "charge.chargedback" or "charge.chargeback";
}
