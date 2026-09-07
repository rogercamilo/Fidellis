using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Reports;

/// <summary>
/// Snapshots/assinatura das demonstrações (RF-FIN-173 / DT-10): gera a versão congelada de um período,
/// lista e aprova (assina) a prestação de contas. Gravações bloqueadas para perfis somente-leitura; a
/// aprovação exige papel de governança (admin/conselho fiscal).
/// </summary>
public static class StatementSnapshotEndpoints
{
    public static IEndpointRouteBuilder MapReportSnapshots(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/reports/snapshots").WithTags("Finance/Reports")
            .AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (int? year, StatementSnapshotService svc, CancellationToken ct) =>
            Results.Ok((await svc.ListAsync(year, ct)).Select(ToDto)));

        g.MapGet("/{id:guid}", async (Guid id, StatementSnapshotService svc, CancellationToken ct) =>
            await svc.GetAsync(id, ct) is { } s ? Results.Ok(ToDetailDto(s)) : Results.NotFound());

        // Congela um snapshot draft do exercício.
        g.MapPost("/", async (GenerateSnapshotRequest req, StatementSnapshotService svc, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            var s = await svc.GenerateAsync(req.Year, user.UserId?.ToString(), ct);
            await audit.RecordAsync("statement_snapshot.generated", "statement_snapshot", $"{s.Id}:{s.Year}");
            return Results.Created($"/api/finance/reports/snapshots/{s.Id}", ToDetailDto(s));
        });

        // Aprova/assina (governança): admin ou conselho fiscal.
        g.MapPost("/{id:guid}/approve", async (Guid id, StatementSnapshotService svc, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            if (!IsSigner(user.Role))
                return Results.Json(new { error = "Somente admin ou conselho fiscal aprova a prestação de contas." },
                    statusCode: StatusCodes.Status403Forbidden);
            try
            {
                var s = await svc.ApproveAsync(id, user.UserId?.ToString() ?? "desconhecido", ct);
                if (s is null) return Results.NotFound();
                await audit.RecordAsync("statement_snapshot.approved", "statement_snapshot", $"{s.Id}:{s.Year}");
                return Results.Ok(ToDetailDto(s));
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }

    private static bool IsSigner(string? role)
        => string.Equals(role, FinanceRoles.Admin, StringComparison.OrdinalIgnoreCase)
        || string.Equals(role, FinanceRoles.FiscalCouncil, StringComparison.OrdinalIgnoreCase);

    private static SnapshotDto ToDto(StatementSnapshot s)
        => new(s.Id, s.Year, s.Quarter, s.Status, s.Hash, s.GeneratedBy, s.ApprovedBy, s.ApprovedAt, s.CreatedAt);

    private static SnapshotDetailDto ToDetailDto(StatementSnapshot s)
        => new(s.Id, s.Year, s.Quarter, s.Status, s.Hash, s.GeneratedBy, s.ApprovedBy, s.ApprovedAt, s.CreatedAt, s.Payload);
}

public sealed record GenerateSnapshotRequest(int Year);
public sealed record SnapshotDto(Guid Id, int Year, int? Quarter, string Status, string Hash,
    string? GeneratedBy, string? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset CreatedAt);
public sealed record SnapshotDetailDto(Guid Id, int Year, int? Quarter, string Status, string Hash,
    string? GeneratedBy, string? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset CreatedAt, string Payload);
