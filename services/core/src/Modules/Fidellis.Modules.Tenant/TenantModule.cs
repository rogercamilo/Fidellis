using Fidellis.Infrastructure.Catalog;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Provisioning;
using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
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
            TenantDbContext tenantDb,
            ITenantContext tenantContext,
            ISchemaProvisioner provisioner,
            Infrastructure.Accounting.ChartOfAccountsSeeder chartSeeder,
            Infrastructure.Dimensions.DimensionsSeeder dimensionsSeeder,
            Infrastructure.Configuration.FinanceConfigSeeder financeConfigSeeder,
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

            // 3) semeia o plano de contas + dimensões + configuração financeira default do tenant
            tenantContext.SetTenant(slug);
            await chartSeeder.EnsureDefaultAsync(ct);
            await dimensionsSeeder.EnsureDefaultsAsync(ct);
            await financeConfigSeeder.EnsureDefaultsAsync(ct);

            // 4) opcional: vincula o primeiro usuário (admin) e cria a organização-raiz,
            //    já associando o admin a ela — evita depender de seed manual.
            Guid? rootOrganizationId = null;
            if (req.AdminUserId is { } adminId)
            {
                db.Memberships.Add(new Membership { UserId = adminId, TenantId = tenant.Id, Role = "admin" });
                await db.SaveChangesAsync(ct);

                var rootOrg = new Organization { Name = (req.OrganizationName ?? req.Name).Trim() };
                tenantDb.Organizations.Add(rootOrg);
                tenantDb.OrgMembers.Add(new OrgMember { UserId = adminId, OrganizationId = rootOrg.Id, Role = "admin" });
                await tenantDb.SaveChangesAsync(ct);
                rootOrganizationId = rootOrg.Id;
            }

            var dto = new TenantDto(tenant.Id, tenant.Slug, tenant.Name, schema, tenant.Plan, tenant.Status, rootOrganizationId);
            return Results.Created($"/api/tenants/{tenant.Slug}", dto);
        });

        return app;
    }
}

public sealed record CreateTenantRequest(string Slug, string Name, Guid? AdminUserId = null, string? OrganizationName = null);

public sealed record TenantDto(Guid Id, string Slug, string Name, string SchemaName, string Plan, string Status, Guid? RootOrganizationId = null);
