using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Linha de um <see cref="BankStatement"/> (transação do extrato). O sinal do <see cref="Amount"/>
/// segue o extrato (+ entrada / − saída). A conciliação (inc.3.1) casa a linha com recebível/pagável/
/// movimento de tesouraria. Reside no schema do tenant.
/// </summary>
public sealed class BankStatementLine : Entity
{
    public required Guid StatementId { get; set; }

    /// <summary>Id da transação no extrato (dedupe entre reimportações).</summary>
    public string? FitId { get; set; }

    public required DateOnly PostedAt { get; set; }
    public required decimal Amount { get; set; }
    public string? Memo { get; set; }

    /// <summary>unmatched | partial | matched | ignored (DT-08: <c>partial</c> = casada em parte).</summary>
    public string Status { get; set; } = "unmatched";

    /// <summary>receivable | payable | movement (quando conciliada). No N:1, o tipo do 1º casamento.</summary>
    public string? MatchedType { get; set; }
    public Guid? MatchedId { get; set; }

    /// <summary>
    /// Valor já casado da linha (DT-08). Permite baixa parcial, N:1 (uma linha quita vários títulos) e
    /// 1:N (vários lançamentos quitam um título). A linha vira <c>matched</c> quando atinge |Amount|.
    /// </summary>
    public decimal MatchedAmount { get; set; }
}
