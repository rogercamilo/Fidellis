using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Dimensions;

/// <summary>
/// Endpoints de configuração das dimensões gerenciais (centros de custo, fundos e projetos) —
/// Sub-bloco F da Onda 1. Todos operam no schema do tenant resolvido por request.
/// </summary>
public static class DimensionsEndpoints
{
    public static IEndpointRouteBuilder MapDimensions(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance").WithTags("Finance/Dimensions");

        // ---- Centros de custo ----
        g.MapGet("/cost-centers", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.CostCenters.OrderBy(c => c.Code)
                .Select(c => new CostCenterDto(c.Id, c.Code, c.Name, c.IsDefault, c.Active)).ToListAsync(ct)));

        g.MapPost("/cost-centers", async (
            UpsertCostCenterRequest req, TenantDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "code e name são obrigatórios." });
            var code = req.Code.Trim().ToUpperInvariant();
            if (await db.CostCenters.AnyAsync(c => c.Code == code, ct))
                return Results.Conflict(new { error = $"Centro de custo '{code}' já existe." });

            var cc = new CostCenter { Code = code, Name = req.Name.Trim() };
            db.CostCenters.Add(cc);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/cost-centers/{cc.Id}",
                new CostCenterDto(cc.Id, cc.Code, cc.Name, cc.IsDefault, cc.Active));
        });

        g.MapPatch("/cost-centers/{id:guid}", async (
            Guid id, PatchDimensionRequest req, TenantDbContext db, CancellationToken ct) =>
        {
            var cc = await db.CostCenters.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (cc is null) return Results.NotFound();
            if (req.Name is { Length: > 0 }) cc.Name = req.Name.Trim();
            if (req.Active is { } active) cc.Active = active;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new CostCenterDto(cc.Id, cc.Code, cc.Name, cc.IsDefault, cc.Active));
        });

        // ---- Fundos (com/sem restrição) ----
        g.MapGet("/funds", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Funds.OrderBy(f => f.Code)
                .Select(f => new FundDto(f.Id, f.Code, f.Name, f.Restriction, f.Purpose, f.IsDefault, f.Active)).ToListAsync(ct)));

        g.MapPost("/funds", async (
            UpsertFundRequest req, TenantDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "code e name são obrigatórios." });
            var restriction = (req.Restriction ?? "free").Trim().ToLowerInvariant();
            if (restriction is not ("free" or "restricted"))
                return Results.BadRequest(new { error = "restriction deve ser 'free' ou 'restricted'." });
            if (restriction == "restricted" && string.IsNullOrWhiteSpace(req.Purpose))
                return Results.BadRequest(new { error = "purpose é obrigatório para fundo com restrição (ITG 2002)." });
            var code = req.Code.Trim().ToUpperInvariant();
            if (await db.Funds.AnyAsync(f => f.Code == code, ct))
                return Results.Conflict(new { error = $"Fundo '{code}' já existe." });

            var fund = new Fund { Code = code, Name = req.Name.Trim(), Restriction = restriction, Purpose = req.Purpose?.Trim() };
            db.Funds.Add(fund);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/funds/{fund.Id}",
                new FundDto(fund.Id, fund.Code, fund.Name, fund.Restriction, fund.Purpose, fund.IsDefault, fund.Active));
        });

        g.MapPatch("/funds/{id:guid}", async (
            Guid id, PatchFundRequest req, TenantDbContext db, CancellationToken ct) =>
        {
            var fund = await db.Funds.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (fund is null) return Results.NotFound();
            if (req.Name is { Length: > 0 }) fund.Name = req.Name.Trim();
            if (req.Purpose is not null) fund.Purpose = req.Purpose.Trim();
            if (req.Active is { } active) fund.Active = active;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new FundDto(fund.Id, fund.Code, fund.Name, fund.Restriction, fund.Purpose, fund.IsDefault, fund.Active));
        });

        // ---- Projetos ----
        g.MapGet("/projects", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Projects.OrderBy(p => p.Code)
                .Select(p => new ProjectDto(p.Id, p.Code, p.Name, p.FundId, p.BudgetAmount, p.StartsAt, p.EndsAt, p.Status)).ToListAsync(ct)));

        g.MapPost("/projects", async (
            UpsertProjectRequest req, TenantDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "code e name são obrigatórios." });
            var code = req.Code.Trim().ToUpperInvariant();
            if (await db.Projects.AnyAsync(p => p.Code == code, ct))
                return Results.Conflict(new { error = $"Projeto '{code}' já existe." });
            if (req.FundId is { } fundId && !await db.Funds.AnyAsync(f => f.Id == fundId, ct))
                return Results.BadRequest(new { error = "fundId inexistente." });

            var project = new Project
            {
                Code = code, Name = req.Name.Trim(), FundId = req.FundId,
                BudgetAmount = req.BudgetAmount, StartsAt = req.StartsAt, EndsAt = req.EndsAt,
            };
            db.Projects.Add(project);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/projects/{project.Id}",
                new ProjectDto(project.Id, project.Code, project.Name, project.FundId, project.BudgetAmount, project.StartsAt, project.EndsAt, project.Status));
        });

        g.MapPatch("/projects/{id:guid}", async (
            Guid id, PatchProjectRequest req, TenantDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (project is null) return Results.NotFound();
            if (req.Name is { Length: > 0 }) project.Name = req.Name.Trim();
            if (req.BudgetAmount is { } budget) project.BudgetAmount = budget;
            if (req.Status is { Length: > 0 }) project.Status = req.Status.Trim();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new ProjectDto(project.Id, project.Code, project.Name, project.FundId, project.BudgetAmount, project.StartsAt, project.EndsAt, project.Status));
        });

        return app;
    }
}

public sealed record CostCenterDto(Guid Id, string Code, string Name, bool IsDefault, bool Active);
public sealed record FundDto(Guid Id, string Code, string Name, string Restriction, string? Purpose, bool IsDefault, bool Active);
public sealed record ProjectDto(Guid Id, string Code, string Name, Guid? FundId, decimal? BudgetAmount, DateOnly? StartsAt, DateOnly? EndsAt, string Status);

public sealed record UpsertCostCenterRequest(string Code, string Name);
public sealed record UpsertFundRequest(string Code, string Name, string? Restriction = null, string? Purpose = null);
public sealed record UpsertProjectRequest(string Code, string Name, Guid? FundId = null, decimal? BudgetAmount = null, DateOnly? StartsAt = null, DateOnly? EndsAt = null);
public sealed record PatchDimensionRequest(string? Name = null, bool? Active = null);
public sealed record PatchFundRequest(string? Name = null, string? Purpose = null, bool? Active = null);
public sealed record PatchProjectRequest(string? Name = null, decimal? BudgetAmount = null, string? Status = null);
