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

    /// <summary>unmatched | matched | ignored.</summary>
    public string Status { get; set; } = "unmatched";

    /// <summary>receivable | payable | movement (quando conciliada).</summary>
    public string? MatchedType { get; set; }
    public Guid? MatchedId { get; set; }
}
