using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Mensagem da régua de relacionamento (outbox). Enfileirada por eventos (agradecimento, dunning,
/// past_due, reativação) e despachada por canal. <see cref="DedupeKey"/> garante idempotência.
/// </summary>
public sealed class OutboxMessage : Entity
{
    public Guid? DonorId { get; set; }

    /// <summary>email | whatsapp.</summary>
    public string Channel { get; set; } = "email";

    public required string EventType { get; set; }
    public required string Template { get; set; }
    public required string ToAddress { get; set; }
    public string? Subject { get; set; }
    public required string Body { get; set; }

    /// <summary>queued | sent | failed | skipped.</summary>
    public string Status { get; set; } = "queued";

    public int Attempts { get; set; }
    public string? DedupeKey { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? SentAt { get; set; }
}
