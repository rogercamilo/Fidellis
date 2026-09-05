using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Donations;

/// <summary>
/// Módulo Donations — organizations (unidades Rede→Unidade) + campanhas/doações/doadores.
/// Neste passo expõe o CRUD mínimo de organizations para alimentar os formulários de cobrança.
/// </summary>
public static class DonationsModule
{
    public static IServiceCollection AddDonationsModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapDonationsModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/donations").WithTags("Donations");

        // Demonstra o acesso por schema do tenant: conta doações no schema resolvido.
        group.MapGet("/ping", async (
            ITenantContext tenant,
            TenantDbContext db,
            CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request (header X-Tenant ou claim)." });

            var count = await db.Donations.CountAsync(ct);
            return Results.Ok(new { module = "Donations", tenant = tenant.TenantId, schema = tenant.SchemaName, donations = count });
        });

        // Unidades (organizations) do tenant — usadas pelos formulários de cobrança/recorrência.
        var orgs = app.MapGroup("/api/organizations").WithTags("Organizations");

        orgs.MapGet("/", async (ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });

            var list = await db.Organizations
                .OrderBy(o => o.Name)
                .Select(o => new { id = o.Id, name = o.Name, parentId = o.ParentId })
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        orgs.MapPost("/", async (CreateOrganizationRequest req, ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name é obrigatório." });

            var org = new Organization { Name = req.Name.Trim(), ParentId = req.ParentId };
            db.Organizations.Add(org);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/organizations/{org.Id}", new { id = org.Id, name = org.Name, parentId = org.ParentId });
        });

        return app;
    }
}

public sealed record CreateOrganizationRequest(string Name, Guid? ParentId = null);
