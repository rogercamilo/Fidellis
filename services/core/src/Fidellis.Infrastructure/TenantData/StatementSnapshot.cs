using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>
/// Versão <b>congelada</b> das demonstrações de um período (RF-FIN-173 / DT-10): o rascunho on-the-fly
/// é serializado, tem um <c>Hash</c> (SHA-256 do payload) e um fluxo de aprovação/assinatura da
/// prestação de contas. Uma vez <c>approved</c>, o conteúdo não muda mais (fonte para o histórico).
/// </summary>
public sealed class StatementSnapshot : Entity
{
    public required int Year { get; set; }

    /// <summary>Trimestre (1–4) ou <c>null</c> para o exercício anual.</summary>
    public int? Quarter { get; set; }

    /// <summary>JSON das demonstrações congeladas (DRP, Balanço, DFC, segregação/DMPL).</summary>
    public required string Payload { get; set; }

    /// <summary>SHA-256 (hex) do payload — a "assinatura" de integridade do congelamento.</summary>
    public required string Hash { get; set; }

    /// <summary><c>draft</c> (gerado) ou <c>approved</c> (assinado/aprovado).</summary>
    public string Status { get; set; } = "draft";

    public string? GeneratedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
