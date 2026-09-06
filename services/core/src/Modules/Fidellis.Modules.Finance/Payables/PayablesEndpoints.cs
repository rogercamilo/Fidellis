using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Payables;

/// <summary>
/// Endpoints de Contas a Pagar — base (Onda 2 inc.2.2): credores, títulos com rateio, cancelamento.
/// Aprovação/pagamento entram no inc.2.3. Mutações passam pelo <see cref="FinanceWriteFilter"/>.
/// </summary>
public static class PayablesEndpoints
{
    public static IEndpointRouteBuilder MapPayables(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance").WithTags("Finance/Payables").AddEndpointFilter<FinanceWriteFilter>();

        // ---- Credores ----
        g.MapGet("/payees", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Payees.OrderBy(p => p.Name)
                .Select(p => new PayeeDto(p.Id, p.Name, p.Document, p.PixKey, p.Kind, p.Active)).ToListAsync(ct)));

        g.MapPost("/payees", async (
            CreatePayeeRequest req, PayablesService payables, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name é obrigatório." });
            var p = await payables.CreatePayeeAsync(req.Name.Trim(), req.Document, req.PixKey, req.Kind ?? "supplier", ct);
            return Results.Created($"/api/finance/payees/{p.Id}", new PayeeDto(p.Id, p.Name, p.Document, p.PixKey, p.Kind, p.Active));
        });

        // ---- Títulos a pagar ----
        g.MapGet("/payables", async (string? status, TenantDbContext db, CancellationToken ct) =>
        {
            var q = db.Payables.AsQueryable();
            if (status is { Length: > 0 }) q = q.Where(p => p.Status == status);
            var list = await q.OrderBy(p => p.DueDate)
                .Select(p => new PayableDto(p.Id, p.PayeeId, p.Description, p.Amount, p.DueDate, p.Status, p.CategoryId, p.PaidAt))
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        g.MapPost("/payables", async (
            CreatePayableRequest req, PayablesService payables, ITenantContext tenant, ICurrentUser user, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (req.PayeeId == Guid.Empty || req.Amount <= 0 || string.IsNullOrWhiteSpace(req.Description))
                return Results.BadRequest(new { error = "payeeId, amount (>0) e description são obrigatórios." });
            try
            {
                var allocations = req.Allocations?
                    .Select(a => new PayableAllocationInput(a.Amount, a.CostCenterId, a.ProjectId, a.FundId)).ToList();
                var p = await payables.CreatePayableAsync(req.PayeeId, req.Amount, req.DueDate, req.Description.Trim(),
                    req.CategoryId, req.DocumentUrl, req.CostCenterId, req.ProjectId, req.FundId, allocations, user.UserId, ct);
                return Results.Created($"/api/finance/payables/{p.Id}",
                    new PayableDto(p.Id, p.PayeeId, p.Description, p.Amount, p.DueDate, p.Status, p.CategoryId, p.PaidAt));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        g.MapPost("/payables/{id:guid}/cancel", async (Guid id, PayablesService payables, CancellationToken ct) =>
        {
            try
            {
                var p = await payables.CancelAsync(id, ct);
                return p is null ? Results.NotFound() : Results.Ok(new { id = p.Id, status = p.Status });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ---- Alçadas: aprovar / rejeitar / pagar (RF-FIN-112/113) ----
        g.MapPost("/payables/{id:guid}/approve", async (
            Guid id, ApprovalService approvals, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid)
                return Results.BadRequest(new { error = "Usuário do request não identificado." });
            try
            {
                var p = await approvals.ApproveAsync(id, uid, user.Role ?? "", ct);
                return Results.Ok(new { id = p.Id, status = p.Status, approvedAt = p.ApprovedAt });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        g.MapPost("/payables/{id:guid}/reject", async (
            Guid id, ApprovalService approvals, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } uid)
                return Results.BadRequest(new { error = "Usuário do request não identificado." });
            try
            {
                var p = await approvals.RejectAsync(id, uid, user.Role ?? "", ct);
                return Results.Ok(new { id = p.Id, status = p.Status });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        g.MapPost("/payables/{id:guid}/pay", async (
            Guid id, PayPayableRequest req, PayablesService payables, CancellationToken ct) =>
        {
            try
            {
                var p = await payables.PayAsync(id, req.TreasuryAccountId, ct);
                return p is null ? Results.NotFound() : Results.Ok(new { id = p.Id, status = p.Status, paidAt = p.PaidAt });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // ---- Configuração das faixas de alçada (RF-FIN-112) ----
        g.MapGet("/approval-tiers", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.ApprovalTiers.OrderBy(t => t.MinAmount)
                .Select(t => new ApprovalTierDto(t.Id, t.MinAmount, t.MaxAmount, t.Signatures, t.RolesCsv)).ToListAsync(ct)));

        return app;
    }
}

public sealed record PayeeDto(Guid Id, string Name, string? Document, string? PixKey, string Kind, bool Active);
public sealed record PayableDto(Guid Id, Guid PayeeId, string Description, decimal Amount, DateOnly DueDate, string Status, Guid? CategoryId, DateTimeOffset? PaidAt);

public sealed record CreatePayeeRequest(string Name, string? Document = null, string? PixKey = null, string? Kind = null);
public sealed record AllocationInput(decimal Amount, Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null);
public sealed record CreatePayableRequest(
    Guid PayeeId, decimal Amount, DateOnly DueDate, string Description, Guid? CategoryId = null, string? DocumentUrl = null,
    Guid? CostCenterId = null, Guid? ProjectId = null, Guid? FundId = null, List<AllocationInput>? Allocations = null);
public sealed record PayPayableRequest(Guid TreasuryAccountId);
public sealed record ApprovalTierDto(Guid Id, decimal MinAmount, decimal? MaxAmount, int Signatures, string RolesCsv);
