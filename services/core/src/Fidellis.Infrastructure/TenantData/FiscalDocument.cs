using Fidellis.SharedKernel;

namespace Fidellis.Infrastructure.TenantData;

/// <summary>Tipos de documento fiscal registrados (#79 — NF/faturamento, gestão avançada).</summary>
public static class FiscalDocumentTypes
{
    /// <summary>Nota Fiscal de Serviço eletrônica (municipal).</summary>
    public const string Nfse = "nfse";

    /// <summary>Nota Fiscal eletrônica de produto (modelo 55).</summary>
    public const string Nfe = "nfe";

    public static bool IsValid(string? type) => type is Nfse or Nfe;
}

/// <summary>Origens de recebível que admitem nota fiscal (atividade comercial — não doação).</summary>
public static class FiscalReceivableSources
{
    public const string Service = "service";
    public const string Sale = "sale";

    /// <summary>Um recebível é "faturável" (emite NF) só quando é serviço prestado ou venda.</summary>
    public static bool IsInvoiceable(string? source) => source is Service or Sale;
}

/// <summary>
/// Documento fiscal (NF-e/NFS-e) <b>registrado</b> pela instituição (#79 — reservado atrás de
/// <see cref="FinanceSettings.AdvancedManagement"/>). MVP de <b>registro/vínculo</b>: a nota é emitida por
/// fora (contador/emissor municipal) e o Fidellis guarda número/série/valor/PDF, vinculada ao
/// <see cref="Receivable"/> comercial (serviço/venda) — <b>nunca</b> a doação/dízimo/oferta (essas usam
/// <see cref="Receipt"/>). Emissão integrada via provedor fiscal fica para uma fase futura. Roda no schema do tenant.
/// </summary>
public sealed class FiscalDocument : Entity
{
    public required Guid OrganizationId { get; set; }

    /// <summary><see cref="FiscalDocumentTypes"/>: <c>nfse</c> | <c>nfe</c>.</summary>
    public required string Type { get; set; }

    public required string Number { get; set; }
    public string? Series { get; set; }

    /// <summary>Chave de acesso (NF-e) ou protocolo/verificação (NFS-e), quando houver.</summary>
    public string? AccessKey { get; set; }

    public string? Description { get; set; }
    public required decimal Amount { get; set; }
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Recebível comercial (serviço/venda) que esta nota documenta. Vínculo obrigatório do registro.</summary>
    public required Guid ReceivableId { get; set; }

    /// <summary>Entrada (receita) correspondente, quando já conciliada — para relatório/exportação.</summary>
    public Guid? EntryId { get; set; }

    /// <summary>Estado do registro. No MVP nasce <c>registered</c>; <c>canceled</c> ao cancelar.</summary>
    public string Status { get; set; } = "registered";

    /// <summary>Chave do PDF arquivado no object storage (R2), quando anexado.</summary>
    public string? PdfObjectKey { get; set; }
}
