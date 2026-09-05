using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Processa eventos de webhook do PSP de forma <b>idempotente</b> (dedupe por
/// <c>provider_event_id</c>). Ao confirmar o pagamento, reconsulta o PSP (fonte de verdade),
/// marca a doação como paga e faz a conciliação: cria a <c>transaction</c> e os lançamentos
/// contábeis de partida dobrada. Deve rodar com o <c>ITenantContext</c> já resolvido.
/// </summary>
public sealed class WebhookProcessor(
    TenantDbContext db,
    IPaymentGateway gateway,
    ILogger<WebhookProcessor> logger)
{
    private const string LedgerReceivable = "1.1 PIX a receber";
    private const string LedgerDonations = "3.1 Doações";

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

        // Conta da organization (cria uma padrão se ainda não existir).
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.OrganizationId == donation.OrganizationId, ct);
        if (account is null)
        {
            account = new Account { OrganizationId = donation.OrganizationId, Name = "Conta principal" };
            db.Accounts.Add(account);
        }

        var transaction = new Transaction
        {
            AccountId = account.Id,
            Amount = donation.Amount,
            Kind = "credit",
            Description = $"Doação {donation.Id}",
        };
        db.Transactions.Add(transaction);

        // Partida dobrada: débito em "PIX a receber", crédito em "Doações" (soma balanceada).
        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, Ledger = LedgerReceivable, Debit = donation.Amount, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, Ledger = LedgerDonations, Debit = 0, Credit = donation.Amount });

        logger.LogInformation("Doação {DonationId} conciliada (R$ {Amount}).", donation.Id, donation.Amount);
    }

    private async Task MarkFailedAsync(string chargeId, CancellationToken ct)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(d => d.PspChargeId == chargeId, ct);
        if (donation is not null && donation.Status != "paid")
            donation.Status = "failed";
    }

    private static bool IsPaidEvent(string type)
        => type is "charge.paid" or "order.paid";

    private static bool IsFailedEvent(string type)
        => type is "charge.payment_failed" or "order.payment_failed";
}
