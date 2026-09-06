using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Extrato bancário importado (uma importação) de uma conta de tesouraria — Onda 3 (conciliação).
/// Reside no schema do tenant.
/// </summary>
public sealed class BankStatement : Entity
{
    public required Guid AccountId { get; set; }

    /// <summary>ofx | cnab.</summary>
    public required string Format { get; set; }

    public string? Reference { get; set; }
    public DateTimeOffset ImportedAt { get; set; } = DateTimeOffset.UtcNow;
}
