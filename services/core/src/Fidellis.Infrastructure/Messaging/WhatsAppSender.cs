using Fidellis.Infrastructure.TenantData;
using Microsoft.Extensions.Logging;

namespace Fidellis.Infrastructure.Messaging;

/// <summary>
/// Adapter de WhatsApp (stub). A régua já enfileira mensagens deste canal; o envio real (Meta Cloud
/// API / Twilio BSP, com templates aprovados) entra quando houver conta configurada.
/// </summary>
public sealed class WhatsAppSender(ILogger<WhatsAppSender> logger) : IMessageSender
{
    public string Channel => "whatsapp";

    public Task<SendResult> SendAsync(OutboxMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("[whatsapp:skipped] (stub) para {To}: {Subject}", message.ToAddress, message.Subject);
        return Task.FromResult(SendResult.Skipped("WhatsApp não configurado"));
    }
}
