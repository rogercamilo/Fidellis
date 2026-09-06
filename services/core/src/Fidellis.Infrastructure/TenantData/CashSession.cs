using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Sessão de caixa físico (RF-FIN-132): coleta/oferta em espécie (ex.: missa/culto). Abre num caixa
/// (conta de tesouraria <c>kind=cash</c>), fecha com o valor conferido e a <b>dupla conferência</b>
/// (um segundo responsável, ≠ de quem abriu). O depósito vira transferência para a conta bancária.
/// Reside no schema do tenant.
/// </summary>
public sealed class CashSession : Entity
{
    public required Guid AccountId { get; set; }
    public required Guid OpenedBy { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? EventLabel { get; set; }

    /// <summary>Valor conferido no fechamento.</summary>
    public decimal? CountedAmount { get; set; }

    /// <summary>Segundo responsável que confere o fechamento (dupla conferência).</summary>
    public Guid? ConfirmedBy { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>open | closed.</summary>
    public string Status { get; set; } = "open";

    /// <summary>Movimento de saída (transferência) gerado no depósito para a conta bancária.</summary>
    public Guid? DepositedMovementId { get; set; }
}
