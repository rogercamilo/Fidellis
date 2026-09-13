using System.Globalization;
using System.Text;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Storage;
using Fidellis.Infrastructure.TenantData;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Services;

/// <summary>Erro de regra de negócio do faturamento (mapeado a 400/409 no endpoint).</summary>
public sealed class FiscalDocumentException(string message) : Exception(message);

/// <summary>
/// Documentos fiscais (#79 — NF/faturamento). MVP de <b>registro/vínculo</b>: registra uma NF (NFS-e/NF-e)
/// emitida por fora, vinculada a um recebível <b>comercial</b> (serviço/venda), e arquiva o PDF no object
/// storage. Todo o recurso é gated por <see cref="FinanceSettings.AdvancedManagement"/> (D-09). Roda no
/// schema do tenant. Emissão integrada via provedor fiscal fica como evolução (fase 2).
/// </summary>
public sealed class FiscalDocumentService(TenantDbContext db, IObjectStorage storage)
{
    public async Task<FiscalDocument> RegisterAsync(
        Guid organizationId, string type, string number, string? series, string? accessKey,
        decimal amount, DateTimeOffset? issuedAt, Guid receivableId, string? description, CancellationToken ct = default)
    {
        await EnsureAdvancedAsync(ct);

        var normalizedType = (type ?? "").Trim().ToLowerInvariant();
        if (!FiscalDocumentTypes.IsValid(normalizedType))
            throw new FiscalDocumentException("type deve ser 'nfse' (serviço) ou 'nfe' (produto).");
        if (string.IsNullOrWhiteSpace(number))
            throw new FiscalDocumentException("number é obrigatório.");
        if (amount <= 0)
            throw new FiscalDocumentException("amount deve ser positivo.");

        var receivable = await db.Receivables.FirstOrDefaultAsync(r => r.Id == receivableId, ct)
            ?? throw new FiscalDocumentException("Recebível não encontrado.");
        if (!FiscalReceivableSources.IsInvoiceable(receivable.Source))
            throw new FiscalDocumentException(
                "Nota fiscal só se aplica a recebível comercial (source 'service' ou 'sale'); doações usam recibo.");

        var doc = new FiscalDocument
        {
            OrganizationId = organizationId,
            Type = normalizedType,
            Number = number.Trim(),
            Series = series?.Trim(),
            AccessKey = accessKey?.Trim(),
            Description = description?.Trim(),
            Amount = amount,
            IssuedAt = issuedAt ?? DateTimeOffset.UtcNow,
            ReceivableId = receivableId,
        };
        db.FiscalDocuments.Add(doc);
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<FiscalDocument?> AttachPdfAsync(Guid id, byte[] content, CancellationToken ct = default)
    {
        await EnsureAdvancedAsync(ct);
        var doc = await db.FiscalDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null) return null;
        if (content.Length == 0) throw new FiscalDocumentException("PDF vazio.");

        var key = $"fiscal/{doc.OrganizationId:N}/{doc.Id:N}.pdf";
        await storage.PutAsync(key, content, "application/pdf", ct);
        doc.PdfObjectKey = key;
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<FiscalDocument?> CancelAsync(Guid id, CancellationToken ct = default)
    {
        await EnsureAdvancedAsync(ct);
        var doc = await db.FiscalDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc is null) return null;
        doc.Status = "canceled";
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task<byte[]?> GetPdfAsync(Guid id, CancellationToken ct = default)
    {
        var doc = await db.FiscalDocuments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (doc?.PdfObjectKey is not { Length: > 0 } key) return null;
        return await storage.GetAsync(key, ct);
    }

    public async Task<List<FiscalDocument>> ListAsync(CancellationToken ct = default)
        => await db.FiscalDocuments.OrderByDescending(d => d.IssuedAt).ToListAsync(ct);

    /// <summary>CSV das notas do ano para o contador (separador <c>;</c>).</summary>
    public async Task<string> ExportCsvAsync(int year, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddYears(1);
        var rows = await db.FiscalDocuments
            .Where(d => d.IssuedAt >= start && d.IssuedAt < end)
            .OrderBy(d => d.IssuedAt)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("emissao;tipo;numero;serie;chave;valor;status;recebivel;descricao");
        foreach (var d in rows)
            sb.Append(d.IssuedAt.ToString("yyyy-MM-dd")).Append(';')
              .Append(d.Type).Append(';')
              .Append(Csv(d.Number)).Append(';')
              .Append(Csv(d.Series ?? "")).Append(';')
              .Append(Csv(d.AccessKey ?? "")).Append(';')
              .Append(d.Amount.ToString("0.00", CultureInfo.InvariantCulture)).Append(';')
              .Append(d.Status).Append(';')
              .Append(d.ReceivableId).Append(';')
              .Append(Csv(d.Description ?? "")).Append('\n');
        return sb.ToString();
    }

    /// <summary>Faturamento é recurso avançado (D-09): sem o modo ligado, nenhuma escrita é aceita.</summary>
    private async Task EnsureAdvancedAsync(CancellationToken ct)
    {
        var settings = await db.FinanceSettings.FirstOrDefaultAsync(ct);
        if (settings is null || !settings.AdvancedManagement)
            throw new FiscalDocumentException("Faturamento exige o modo 'Gestão avançada' ligado (Configurações).");
    }

    private static string Csv(string s) => s.Contains(';') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
