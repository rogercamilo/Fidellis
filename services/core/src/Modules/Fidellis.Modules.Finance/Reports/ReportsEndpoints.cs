using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Reports;

/// <summary>
/// Demonstrações contábeis ITG 2002 (Onda 4 inc.4.0): balancete, DRP e Balanço Patrimonial. Somente
/// leitura (liberado a conselho fiscal/contador). O rascunho é gerado on-the-fly (D5).
/// </summary>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/reports").WithTags("Finance/Reports");

        g.MapGet("/trial-balance", async (int year, StatementsService s, CancellationToken ct) =>
        {
            var lines = await s.TrialBalanceAsync(year, ct);
            return Results.Ok(new { year, totalDebit = lines.Sum(l => l.Debit), totalCredit = lines.Sum(l => l.Credit), accounts = lines });
        });

        g.MapGet("/income", async (int year, StatementsService s, CancellationToken ct) =>
            Results.Ok(await s.IncomeAsync(year, ct)));

        g.MapGet("/balance-sheet", async (int year, StatementsService s, CancellationToken ct) =>
            Results.Ok(await s.BalanceSheetAsync(year, ct)));

        // Segregação com/sem restrição (RF-FIN-161) + DMPL.
        g.MapGet("/income-segregated", async (int year, StatementsService s, CancellationToken ct) =>
            Results.Ok(await s.IncomeSegregatedAsync(year, ct)));

        g.MapGet("/dmpl", async (int year, StatementsService s, CancellationToken ct) =>
            Results.Ok(await s.DmplAsync(year, ct)));

        // DFC (método direto) — Onda 4 inc.4.2.
        g.MapGet("/cashflow", async (int year, StatementsService s, CancellationToken ct) =>
            Results.Ok(await s.CashFlowAsync(year, ct)));

        // ---- Trabalho voluntário a valor justo (RF-FIN-162) ----
        var vw = app.MapGroup("/api/finance/volunteer-work").WithTags("Finance/Reports").AddEndpointFilter<FinanceWriteFilter>();

        vw.MapGet("/", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.VolunteerWork.OrderByDescending(v => v.PerformedOn)
                .Select(v => new VolunteerWorkDto(v.Id, v.OrganizationId, v.Description, v.FairValue, v.PerformedOn)).ToListAsync(ct)));

        vw.MapPost("/", async (RecordVolunteerRequest req, VolunteerWorkService svc, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.OrganizationId == Guid.Empty || string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "organizationId e description são obrigatórios." });
            try
            {
                var v = await svc.RecordAsync(req.OrganizationId, req.Description.Trim(), req.FairValue, req.PerformedOn,
                    req.CostCenterId, req.ProjectId, req.FundId, ct);
                return Results.Created($"/api/finance/volunteer-work/{v.Id}",
                    new VolunteerWorkDto(v.Id, v.OrganizationId, v.Description, v.FairValue, v.PerformedOn));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // ---- Prestação de contas MROSC (RF-FIN-163) ----
        app.MapGet("/api/finance/mrosc/{projectId:guid}", async (Guid projectId, MroscReportService svc, CancellationToken ct) =>
            await svc.ReportAsync(projectId, ct) is { } r ? Results.Ok(r) : Results.NotFound())
            .WithTags("Finance/Reports");

        // ---- Exportação para o contador (RF-FIN-165, CSV) ----
        app.MapGet("/api/finance/export/ledger", async (int year, AccountantExportService export, CancellationToken ct) =>
            Results.Text(await export.LedgerCsvAsync(year, ct), "text/csv; charset=utf-8"))
            .WithTags("Finance/Reports");

        app.MapGet("/api/finance/export/trial-balance", async (int year, AccountantExportService export, CancellationToken ct) =>
            Results.Text(await export.TrialBalanceCsvAsync(year, ct), "text/csv; charset=utf-8"))
            .WithTags("Finance/Reports");

        return app;
    }
}

public sealed record VolunteerWorkDto(Guid Id, Guid OrganizationId, string Description, decimal FairValue, DateOnly PerformedOn);
public sealed record RecordVolunteerRequest(Guid OrganizationId, string Description, decimal FairValue, DateOnly PerformedOn,
    Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);
