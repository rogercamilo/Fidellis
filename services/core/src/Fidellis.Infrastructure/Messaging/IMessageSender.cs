using Fidellis.Infrastructure.TenantData;

namespace Fidellis.Infrastructure.Messaging;

/// <summary>Resultado do envio: <c>sent</c> | <c>skipped</c> (sem credencial/canal) | <c>failed</c>.</summary>
public sealed record SendResult(string Status, string? Error = null)
{
    public static readonly SendResult Sent = new("sent");
    public static SendResult Skipped(string reason) => new("skipped", reason);
    public static SendResult Failed(string error) => new("failed", error);
}

/// <summary>Canal de envio (e-mail, WhatsApp, …). O dispatcher resolve por <see cref="Channel"/>.</summary>
public interface IMessageSender
{
    string Channel { get; }
    Task<SendResult> SendAsync(OutboxMessage message, CancellationToken ct = default);
}
