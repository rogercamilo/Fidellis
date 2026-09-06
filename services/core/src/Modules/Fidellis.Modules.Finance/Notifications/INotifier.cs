using Fidellis.Infrastructure.TenantData;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Notifications;

/// <summary>
/// Canal de notificação de cobrança/dunning. No passo 2 há apenas a implementação de log
/// (<see cref="LogNotifier"/>); e-mail/WhatsApp reais entram no passo 4 (CRM/régua).
/// </summary>
public interface INotifier
{
    Task ChargeCreatedAsync(RecurringDonation recurring, Donation cycle, CancellationToken ct = default);
    Task PaymentFailedAsync(RecurringDonation recurring, int attempt, CancellationToken ct = default);
    Task PastDueAsync(RecurringDonation recurring, CancellationToken ct = default);

    /// <summary>Doação confirmada — dispara o agradecimento (com nº do recibo, se houver).</summary>
    Task DonationPaidAsync(Donation donation, string? receiptNumber, CancellationToken ct = default);
}

/// <summary>Implementação stub: registra em log. Encaixe pronto para canais reais no passo 4.</summary>
public sealed class LogNotifier(ILogger<LogNotifier> logger) : INotifier
{
    public Task ChargeCreatedAsync(RecurringDonation recurring, Donation cycle, CancellationToken ct = default)
    {
        logger.LogInformation("[notify] Cobrança gerada p/ recorrência {Id} (R$ {Amount}).", recurring.Id, cycle.Amount);
        return Task.CompletedTask;
    }

    public Task PaymentFailedAsync(RecurringDonation recurring, int attempt, CancellationToken ct = default)
    {
        logger.LogInformation("[notify] Falha de pagamento na recorrência {Id}; tentativa {Attempt}.", recurring.Id, attempt);
        return Task.CompletedTask;
    }

    public Task PastDueAsync(RecurringDonation recurring, CancellationToken ct = default)
    {
        logger.LogInformation("[notify] Recorrência {Id} marcada como inadimplente (past_due).", recurring.Id);
        return Task.CompletedTask;
    }

    public Task DonationPaidAsync(Donation donation, string? receiptNumber, CancellationToken ct = default)
    {
        logger.LogInformation("[notify] Doação {Id} paga — agradecimento (recibo {Receipt}).", donation.Id, receiptNumber);
        return Task.CompletedTask;
    }
}
