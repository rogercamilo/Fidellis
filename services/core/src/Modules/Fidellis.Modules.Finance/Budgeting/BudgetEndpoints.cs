using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Budgeting;

/// <summary>
/// Endpoints de orçamento (Onda 3 inc.3.3): CRUD por dimensão/ano + previsto × realizado. Mutações
/// passam pelo <see cref="FinanceWriteFilter"/>.
/// </summary>
public static class BudgetEndpoints
{
    public static IEndpointRouteBuilder MapBudgets(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/budgets").WithTags("Finance/Budgets").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (int? year, bool? includeInactive, BudgetService budgets, CancellationToken ct) =>
            Results.Ok((await budgets.ListAsync(year, includeInactive ?? false, ct))
                .Select(b => new BudgetDto(b.Id, b.Year, b.Kind, b.Amount, b.CostCenterId, b.ProjectId, b.FundId, b.Revision, b.Active))));

        g.MapGet("/actual", async (int year, BudgetService budgets, CancellationToken ct) =>
            Results.Ok(await budgets.ActualAsync(year, ct)));

        g.MapPost("/{id:guid}/revise", async (Guid id, ReviseBudgetRequest req, BudgetService budgets, CancellationToken ct) =>
        {
            try
            {
                var b = await budgets.ReviseAsync(id, req.Amount, ct);
                return b is null ? Results.NotFound()
                    : Results.Ok(new BudgetDto(b.Id, b.Year, b.Kind, b.Amount, b.CostCenterId, b.ProjectId, b.FundId, b.Revision, b.Active));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapPost("/", async (
            CreateBudgetRequest req, BudgetService budgets, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            try
            {
                var b = await budgets.CreateAsync(req.Year, req.Kind, req.Amount, req.CostCenterId, req.ProjectId, req.FundId, ct);
                return Results.Created($"/api/finance/budgets/{b.Id}",
                    new BudgetDto(b.Id, b.Year, b.Kind, b.Amount, b.CostCenterId, b.ProjectId, b.FundId, b.Revision, b.Active));
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        return app;
    }
}

public sealed record BudgetDto(Guid Id, int Year, string Kind, decimal Amount, Guid? CostCenterId, Guid? ProjectId, Guid? FundId, int Revision, bool Active);
public sealed record CreateBudgetRequest(int Year, string Kind, decimal Amount, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);
public sealed record ReviseBudgetRequest(decimal Amount);
