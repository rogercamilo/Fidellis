using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Provisioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Tenant;

/// <summary>
/// Módulo Tenant — registro e provisionamento de instituições no schema global <c>catalog</c>.
/// Ao criar um tenant, cria também o schema de dados <c>t_&lt;slug&gt;</c>.
/// </summary>
public static class TenantModule
{
    public static IServiceCollection AddTenantModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapTenantModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/tenants").WithTags("Tenant");

        group.MapGet("/", async (CatalogDbContext db, CancellationToken ct) =>
        {
            var tenants = await db.Tenants
                .OrderBy(t => t.Slug)
                .Select(t => new TenantDto(t.Id, t.Slug, t.Name, t.SchemaName, t.Plan, t.Status))
                .ToListAsync(ct);
            return Results.Ok(tenants);
        });

        group.MapPost("/", async (
            CreateTenantRequest req,
            CatalogDbContext db,
            ISchemaProvisioner provisioner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Slug) || string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "slug e name são obrigatórios." });

            var slug = req.Slug.Trim().ToLowerInvariant();
            if (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
                return Results.Conflict(new { error = $"Tenant '{slug}' já existe." });

            // 1) cria o schema de dados do tenant (t_<slug>) + tabelas
            var schema = await provisioner.ProvisionTenantAsync(slug, ct);

            // 2) registra o tenant no catálogo global
            var tenant = Infrastructure.Catalog.Tenant.Create(slug, req.Name);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);

            var dto = new TenantDto(tenant.Id, tenant.Slug, tenant.Name, schema, tenant.Plan, tenant.Status);
            return Results.Created($"/api/tenants/{tenant.Slug}", dto);
        });

        return app;
    }
}

public sealed record CreateTenantRequest(string Slug, string Name);

public sealed record TenantDto(Guid Id, string Slug, string Name, string SchemaName, string Plan, string Status);
