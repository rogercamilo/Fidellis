using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Security;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Modules.Finance.Configuration;

/// <summary>
/// Endpoints de configurabilidade financeira (Sub-bloco J da Onda 1): nomenclatura da doação
/// (recorrente/pontual), tipos de doador e rubricas de receita/despesa. Operam no schema do tenant.
/// </summary>
public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapFinanceConfig(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance").WithTags("Finance/Config").AddEndpointFilter<FinanceWriteFilter>();

        // ---- Nomenclatura (RF-FIN-180/181) ----
        g.MapGet("/settings", async (TenantDbContext db, CancellationToken ct) =>
        {
            var s = await db.FinanceSettings.FirstOrDefaultAsync(ct);
            return Results.Ok(new FinanceSettingsDto(
                s?.RecurringLabel ?? "Dízimo", s?.OnetimeLabel ?? "Oferta"));
        });

        g.MapPut("/settings", async (
            FinanceSettingsDto req, TenantDbContext db, ITenantContext tenant, IClock clock, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.RecurringLabel) || string.IsNullOrWhiteSpace(req.OnetimeLabel))
                return Results.BadRequest(new { error = "recurringLabel e onetimeLabel são obrigatórios." });

            var s = await db.FinanceSettings.FirstOrDefaultAsync(ct);
            if (s is null) { s = new FinanceSettings(); db.FinanceSettings.Add(s); }
            s.RecurringLabel = req.RecurringLabel.Trim();
            s.OnetimeLabel = req.OnetimeLabel.Trim();
            s.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new FinanceSettingsDto(s.RecurringLabel, s.OnetimeLabel));
        });

        // ---- Tipos de doador (RF-FIN-182) ----
        g.MapGet("/donor-types", async (TenantDbContext db, CancellationToken ct) =>
            Results.Ok(await db.DonorTypes.OrderBy(t => t.Name)
                .Select(t => new DonorTypeDto(t.Id, t.Name, t.IsRecurringDefault, t.Active)).ToListAsync(ct)));

        g.MapPost("/donor-types", async (
            UpsertDonorTypeRequest req, TenantDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name é obrigatório." });

            // No máximo um tipo recorrente-default: se este for, desmarca os demais.
            if (req.IsRecurringDefault == true)
                await ClearRecurringDefaultAsync(db, ct);

            var t = new DonorType { Name = req.Name.Trim(), IsRecurringDefault = req.IsRecurringDefault ?? false };
            db.DonorTypes.Add(t);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/donor-types/{t.Id}",
                new DonorTypeDto(t.Id, t.Name, t.IsRecurringDefault, t.Active));
        });

        g.MapPatch("/donor-types/{id:guid}", async (
            Guid id, PatchDonorTypeRequest req, TenantDbContext db, CancellationToken ct) =>
        {
            var t = await db.DonorTypes.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (t is null) return Results.NotFound();
            if (req.Name is { Length: > 0 }) t.Name = req.Name.Trim();
            if (req.Active is { } active) t.Active = active;
            if (req.IsRecurringDefault is { } isDefault)
            {
                if (isDefault) await ClearRecurringDefaultAsync(db, ct);
                t.IsRecurringDefault = isDefault;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new DonorTypeDto(t.Id, t.Name, t.IsRecurringDefault, t.Active));
        });

        // ---- Rubricas de receita/despesa (RF-FIN-183) ----
        g.MapGet("/categories", async (string? kind, TenantDbContext db, CancellationToken ct) =>
        {
            var q = db.FinanceCategories.AsQueryable();
            if (kind is "revenue" or "expense") q = q.Where(c => c.Kind == kind);
            return Results.Ok(await q.OrderBy(c => c.Name)
                .Select(c => new FinanceCategoryDto(c.Id, c.Kind, c.Name, c.LedgerAccountId, c.Active)).ToListAsync(ct));
        });

        g.MapPost("/categories", async (
            UpsertCategoryRequest req, TenantDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var kind = (req.Kind ?? "").Trim().ToLowerInvariant();
            if (kind is not ("revenue" or "expense"))
                return Results.BadRequest(new { error = "kind deve ser 'revenue' ou 'expense'." });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name é obrigatório." });
            if (req.LedgerAccountId is { } la && !await db.LedgerAccounts.AnyAsync(a => a.Id == la, ct))
                return Results.BadRequest(new { error = "ledgerAccountId inexistente." });

            var c = new FinanceCategory { Kind = kind, Name = req.Name.Trim(), LedgerAccountId = req.LedgerAccountId };
            db.FinanceCategories.Add(c);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/finance/categories/{c.Id}",
                new FinanceCategoryDto(c.Id, c.Kind, c.Name, c.LedgerAccountId, c.Active));
        });

        g.MapPatch("/categories/{id:guid}", async (
            Guid id, PatchCategoryRequest req, TenantDbContext db, CancellationToken ct) =>
        {
            var c = await db.FinanceCategories.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (c is null) return Results.NotFound();
            if (req.Name is { Length: > 0 }) c.Name = req.Name.Trim();
            if (req.LedgerAccountId is { } la) c.LedgerAccountId = la;
            if (req.Active is { } active) c.Active = active;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new FinanceCategoryDto(c.Id, c.Kind, c.Name, c.LedgerAccountId, c.Active));
        });

        return app;
    }

    private static async Task ClearRecurringDefaultAsync(TenantDbContext db, CancellationToken ct)
    {
        foreach (var existing in await db.DonorTypes.Where(x => x.IsRecurringDefault).ToListAsync(ct))
            existing.IsRecurringDefault = false;
    }
}

public sealed record FinanceSettingsDto(string RecurringLabel, string OnetimeLabel);
public sealed record DonorTypeDto(Guid Id, string Name, bool IsRecurringDefault, bool Active);
public sealed record FinanceCategoryDto(Guid Id, string Kind, string Name, Guid? LedgerAccountId, bool Active);

public sealed record UpsertDonorTypeRequest(string Name, bool? IsRecurringDefault = null);
public sealed record PatchDonorTypeRequest(string? Name = null, bool? IsRecurringDefault = null, bool? Active = null);
public sealed record UpsertCategoryRequest(string Kind, string Name, Guid? LedgerAccountId = null);
public sealed record PatchCategoryRequest(string? Name = null, Guid? LedgerAccountId = null, bool? Active = null);
