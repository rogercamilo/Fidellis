using Fidellis.Infrastructure.Organizations;
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

        // Todas as unidades do tenant (referência/admin).
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

        // "Minhas unidades": as organizações do usuário + as filiais (descendentes por parent_id).
        orgs.MapGet("/mine", async (ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!user.HasUser)
                return Results.BadRequest(new { error = "Nenhum usuário no request (JWT sub ou header X-User)." });

            var all = await db.Organizations
                .Select(o => new { o.Id, o.ParentId, o.Name })
                .ToListAsync(ct);
            var memberIds = await db.OrgMembers
                .Where(m => m.UserId == user.UserId)
                .Select(m => m.OrganizationId)
                .ToListAsync(ct);

            var visible = OrgVisibility.VisibleOrgIds(memberIds, all.Select(o => (o.Id, o.ParentId)).ToList());

            var result = all.Where(o => visible.Contains(o.Id))
                .OrderBy(o => o.Name)
                .Select(o => new { id = o.Id, name = o.Name, parentId = o.ParentId });
            return Results.Ok(result);
        });

        // Cria uma unidade; o criador (se autenticado) entra como membro admin.
        orgs.MapPost("/", async (CreateOrganizationRequest req, ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (string.IsNullOrWhiteSpace(req.Name))
                return Results.BadRequest(new { error = "name é obrigatório." });

            var org = new Organization { Name = req.Name.Trim(), ParentId = req.ParentId };
            db.Organizations.Add(org);
            if (user.HasUser)
                db.OrgMembers.Add(new OrgMember { UserId = user.UserId!.Value, OrganizationId = org.Id, Role = "admin" });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/organizations/{org.Id}", new { id = org.Id, name = org.Name, parentId = org.ParentId });
        });

        // Vincula um usuário a uma unidade (por padrão, o usuário do request).
        orgs.MapPost("/{id:guid}/members", async (Guid id, AddMemberRequest req, ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant)
                return Results.BadRequest(new { error = "Nenhum tenant no request." });

            var userId = req.UserId ?? user.UserId;
            if (userId is null)
                return Results.BadRequest(new { error = "userId é obrigatório (ou envie um JWT/X-User)." });
            if (!await db.Organizations.AnyAsync(o => o.Id == id, ct))
                return Results.NotFound(new { error = "Unidade não encontrada." });

            if (await db.OrgMembers.AnyAsync(m => m.UserId == userId && m.OrganizationId == id, ct))
                return Results.Ok(new { status = "already_member" });

            db.OrgMembers.Add(new OrgMember { UserId = userId.Value, OrganizationId = id, Role = req.Role ?? "member" });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/organizations/{id}/members", new { organizationId = id, userId, role = req.Role ?? "member" });
        });

        return app;
    }
}

public sealed record CreateOrganizationRequest(string Name, Guid? ParentId = null);

public sealed record AddMemberRequest(Guid? UserId = null, string? Role = null);
