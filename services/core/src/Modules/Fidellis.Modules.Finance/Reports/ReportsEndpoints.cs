using Fidellis.Modules.Finance.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        return app;
    }
}
