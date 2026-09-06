using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Banking;

/// <summary>
/// Endpoints de conciliação — import de extrato (Onda 3 inc.3.0). O conteúdo do arquivo chega como
/// string no corpo JSON (o front lê o arquivo no client) — flui pelo proxy do BFF. Mutações passam
/// pelo <see cref="FinanceWriteFilter"/>. Casamento/baixa entram no inc.3.1.
/// </summary>
public static class StatementEndpoints
{
    public static IEndpointRouteBuilder MapStatements(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/statements").WithTags("Finance/Statements").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.BankStatements.OrderByDescending(s => s.ImportedAt)
                .Select(s => new StatementDto(s.Id, s.AccountId, s.Format, s.Reference, s.ImportedAt)).ToListAsync(ct)));

        g.MapGet("/{id:guid}/lines", async (Guid id, TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.BankStatementLines.Where(l => l.StatementId == id).OrderBy(l => l.PostedAt)
                .Select(l => new StatementLineDto(l.Id, l.FitId, l.PostedAt, l.Amount, l.Memo, l.Status, l.MatchedType, l.MatchedId)).ToListAsync(ct)));

        g.MapPost("/import", async (
            ImportStatementRequest req, StatementImportService import, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.AccountId == Guid.Empty || string.IsNullOrWhiteSpace(req.Content))
                return Results.BadRequest(new { error = "accountId e content são obrigatórios." });
            try
            {
                var (statement, imported, skipped) = await import.ImportAsync(req.AccountId, req.Format ?? "ofx", req.Reference, req.Content, ct);
                return Results.Created($"/api/finance/statements/{statement.Id}", new { id = statement.Id, imported, skipped });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }
}

public sealed record StatementDto(Guid Id, Guid AccountId, string Format, string? Reference, DateTimeOffset ImportedAt);
public sealed record StatementLineDto(Guid Id, string? FitId, DateOnly PostedAt, decimal Amount, string? Memo, string Status, string? MatchedType, Guid? MatchedId);
public sealed record ImportStatementRequest(Guid AccountId, string Content, string? Format = null, string? Reference = null);
