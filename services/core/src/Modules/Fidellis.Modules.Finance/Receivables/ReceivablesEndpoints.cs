using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Receivables;

/// <summary>
/// Endpoints de Contas a Receber (Onda 2 inc.2.1): promessas/recebíveis, baixa manual e aging.
/// Mutações passam pelo <see cref="FinanceWriteFilter"/>.
/// </summary>
public static class ReceivablesEndpoints
{
    public static IEndpointRouteBuilder MapReceivables(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/receivables").WithTags("Finance/Receivables").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (string? status, TenantDbContext db, CancellationToken ct) =>
        {
            var q = db.Receivables.AsQueryable();
            if (status is { Length: > 0 }) q = q.Where(r => r.Status == status);
            var list = await q.OrderBy(r => r.DueDate)
                .Select(r => new ReceivableDto(r.Id, r.OrganizationId, r.DonorId, r.Source, r.Description,
                    r.Amount, r.ReceivedAmount, r.DueDate, r.Status, r.DonationId))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapGet("/aging", async (ReceivablesService receivables, CancellationToken ct) =>
        {
            var r = await receivables.AgingAsync(ct);
            return Results.Ok(new { r.NotDue, r.Overdue1To30, r.Overdue31To60, r.Overdue60Plus, r.TotalOutstanding });
        });

        g.MapPost("/", async (
            CreateReceivableRequest req, ReceivablesService receivables, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.OrganizationId == Guid.Empty || req.Amount <= 0)
                return Results.BadRequest(new { error = "organizationId e amount (>0) são obrigatórios." });

            var r = await receivables.CreateAsync(req.OrganizationId, req.Amount, req.DueDate, req.Source ?? "pledge",
                req.DonorId, req.Description, req.CostCenterId, req.ProjectId, req.FundId, ct);
            return Results.Created($"/api/finance/receivables/{r.Id}",
                new ReceivableDto(r.Id, r.OrganizationId, r.DonorId, r.Source, r.Description, r.Amount, r.ReceivedAmount, r.DueDate, r.Status, r.DonationId));
        });

        g.MapPost("/{id:guid}/settle", async (
            Guid id, SettleReceivableRequest req, ReceivablesService receivables, CancellationToken ct) =>
        {
            if (req.Amount <= 0) return Results.BadRequest(new { error = "amount deve ser positivo." });
            var r = await receivables.SettleAsync(id, req.Amount, req.DonationId, ct);
            return r is null
                ? Results.NotFound()
                : Results.Ok(new ReceivableDto(r.Id, r.OrganizationId, r.DonorId, r.Source, r.Description, r.Amount, r.ReceivedAmount, r.DueDate, r.Status, r.DonationId));
        });

        return app;
    }
}

public sealed record ReceivableDto(
    Guid Id, Guid OrganizationId, Guid? DonorId, string Source, string? Description,
    decimal Amount, decimal ReceivedAmount, DateOnly DueDate, string Status, Guid? DonationId);

public sealed record CreateReceivableRequest(
    Guid OrganizationId, decimal Amount, DateOnly DueDate, string? Source = null, Guid? DonorId = null,
    string? Description = null, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);

public sealed record SettleReceivableRequest(decimal Amount, Guid? DonationId = null);
