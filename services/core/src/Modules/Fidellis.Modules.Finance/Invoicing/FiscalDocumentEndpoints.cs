using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Invoicing;

/// <summary>
/// Faturamento (#79 — NF/faturamento, gestão avançada). Registro/vínculo de NF (NFS-e/NF-e) emitida por
/// fora, atrelada a um recebível comercial, com PDF arquivado no object storage. Escrita passa pelo
/// <see cref="FinanceWriteFilter"/>; o serviço exige o modo "Gestão avançada" (D-09).
/// </summary>
public static class FiscalDocumentEndpoints
{
    public static IEndpointRouteBuilder MapFiscalDocuments(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/fiscal-documents")
            .WithTags("Finance/Invoicing").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (FiscalDocumentService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListAsync(ct)).Select(ToDto)));

        g.MapGet("/export.csv", async (int? year, FiscalDocumentService svc, CancellationToken ct) =>
        {
            var csv = await svc.ExportCsvAsync(year ?? DateTimeOffset.UtcNow.Year, ct);
            return Results.Text(csv, "text/csv");
        });

        g.MapPost("/", async (
            RegisterFiscalDocumentRequest req, FiscalDocumentService svc, ITenantContext tenant,
            IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.OrganizationId == Guid.Empty || req.ReceivableId == Guid.Empty)
                return Results.BadRequest(new { error = "organizationId e receivableId são obrigatórios." });
            try
            {
                var doc = await svc.RegisterAsync(
                    req.OrganizationId, req.Type, req.Number, req.Series, req.AccessKey,
                    req.Amount, req.IssuedAt, req.ReceivableId, req.Description, ct);
                await audit.RecordAsync("fiscal_document.registered", "fiscal_document", doc.Id.ToString());
                return Results.Created($"/api/finance/fiscal-documents/{doc.Id}", ToDto(doc));
            }
            catch (FiscalDocumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapPost("/{id:guid}/pdf", async (
            Guid id, HttpRequest request, FiscalDocumentService svc, IAuditLog audit, CancellationToken ct) =>
        {
            using var ms = new MemoryStream();
            await request.Body.CopyToAsync(ms, ct);
            try
            {
                var doc = await svc.AttachPdfAsync(id, ms.ToArray(), ct);
                if (doc is null) return Results.NotFound();
                await audit.RecordAsync("fiscal_document.pdf_attached", "fiscal_document", id.ToString());
                return Results.Ok(ToDto(doc));
            }
            catch (FiscalDocumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapGet("/{id:guid}/pdf", async (Guid id, FiscalDocumentService svc, CancellationToken ct) =>
        {
            var pdf = await svc.GetPdfAsync(id, ct);
            return pdf is null ? Results.NotFound() : Results.File(pdf, "application/pdf");
        });

        g.MapPost("/{id:guid}/cancel", async (Guid id, FiscalDocumentService svc, IAuditLog audit, CancellationToken ct) =>
        {
            try
            {
                var doc = await svc.CancelAsync(id, ct);
                if (doc is null) return Results.NotFound();
                await audit.RecordAsync("fiscal_document.canceled", "fiscal_document", id.ToString());
                return Results.Ok(ToDto(doc));
            }
            catch (FiscalDocumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }

    private static FiscalDocumentDto ToDto(FiscalDocument d) => new(
        d.Id, d.OrganizationId, d.Type, d.Number, d.Series, d.AccessKey, d.Description,
        d.Amount, d.IssuedAt, d.ReceivableId, d.EntryId, d.Status, d.PdfObjectKey is { Length: > 0 });
}

public sealed record RegisterFiscalDocumentRequest(
    Guid OrganizationId, string Type, string Number, decimal Amount, Guid ReceivableId,
    string? Series = null, string? AccessKey = null, string? Description = null, DateTimeOffset? IssuedAt = null);

public sealed record FiscalDocumentDto(
    Guid Id, Guid OrganizationId, string Type, string Number, string? Series, string? AccessKey,
    string? Description, decimal Amount, DateTimeOffset IssuedAt, Guid ReceivableId, Guid? EntryId,
    string Status, bool HasPdf);
