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

        // Partida dobrada contra o plano de contas (garante o plano p/ tenants antigos).
        await chartSeeder.EnsureDefaultAsync(ct);
        var accounts = await db.LedgerAccounts
            .Where(a => a.Code == ChartOfAccounts.Receivable || a.Code == ChartOfAccounts.Revenue)
            .ToDictionaryAsync(a => a.Code, a => a, ct);
        var receivable = accounts[ChartOfAccounts.Receivable];
        var revenue = accounts[ChartOfAccounts.Revenue];

        db.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = receivable.Id, Ledger = receivable.Name, Debit = donation.Amount, Credit = 0 },
            new AccountingEntry { TransactionId = transaction.Id, LedgerAccountId = revenue.Id, Ledger = revenue.Name, Debit = 0, Credit = donation.Amount });

        // Recibo automático (idempotente por doação).
        var donorDocument = donation.DonorId is { } donorId
            ? await db.Donors.Where(d => d.Id == donorId).Select(d => d.Document).FirstOrDefaultAsync(ct)
            : null;
        var receipt = await receipts.GenerateForDonationAsync(donation, donation.DonorName ?? "Doador", donorDocument, ct);

        // Régua: agradecimento ao doador (enfileira na outbox; envio pelo dispatcher).
        await notifier.DonationPaidAsync(donation, receipt.Number, ct);

        // Ciclo recorrente pago: zera o dunning e agenda a próxima cobrança mensal.
        if (donation.RecurringDonationId is { } recurringId)
        {
            var recurring = await db.RecurringDonations.FirstOrDefaultAsync(r => r.Id == recurringId, ct);
            if (recurring is not null && recurring.Status is "active" or "past_due")
            {
                recurring.Attempt = 0;
                recurring.Status = "active";
                recurring.NextChargeAt = RecurringBillingService.NextChargeDate(recurring.DayOfMonth, clock.UtcNow.AddDays(1));
            }
        }

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
