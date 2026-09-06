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

        // ---- CRM 360º do doador ----
        var crm = app.MapGroup("/api/crm").WithTags("CRM");

        crm.MapGet("/donors", async (ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var rows = await db.Donations
                .Where(d => d.DonorId != null && visible.Contains(d.OrganizationId))
                .Select(d => new { DonorId = d.DonorId!.Value, d.Status, d.Amount, d.PaidAt })
                .ToListAsync(ct);

            var byDonor = rows.GroupBy(r => r.DonorId).ToDictionary(g => g.Key, g => g.ToList());
            var ids = byDonor.Keys.ToList();
            var donors = await db.Donors.Where(d => ids.Contains(d.Id)).ToListAsync(ct);
            var activeRec = (await db.RecurringDonations
                .Where(r => r.Status == "active" && ids.Contains(r.DonorId))
                .Select(r => r.DonorId).ToListAsync(ct)).ToHashSet();

            var window = DateTimeOffset.UtcNow.AddDays(-90);
            var list = donors.Select(donor =>
            {
                var ds = byDonor[donor.Id];
                var paid = ds.Where(x => x.Status == "paid").ToList();
                DateTimeOffset? last = paid.Count > 0 ? paid.Max(x => x.PaidAt) : null;
                var situacao = activeRec.Contains(donor.Id) ? "recorrente"
                    : paid.Count == 0 ? "novo"
                    : last is { } l && l >= window ? "ativo"
                    : "inativo";
                return new
                {
                    id = donor.Id, name = donor.Name, email = donor.Email, document = donor.Document, phone = donor.Phone,
                    totalPaid = paid.Sum(x => x.Amount), donations = paid.Count, lastPaidAt = last, situacao,
                };
            }).OrderByDescending(x => x.totalPaid).ToList();

            return Results.Ok(list);
        });

        crm.MapGet("/donors/{id:guid}", async (Guid id, ITenantContext tenant, ICurrentUser user, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var donor = await db.Donors.FirstOrDefaultAsync(d => d.Id == id, ct);
            if (donor is null) return Results.NotFound();

            var donations = await db.Donations
                .Where(d => d.DonorId == id && visible.Contains(d.OrganizationId))
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new { d.Id, d.Amount, d.Status, d.Method, d.CreatedAt, d.PaidAt })
                .ToListAsync(ct);
            var recurring = await db.RecurringDonations
                .Where(r => r.DonorId == id)
                .Select(r => new { r.Id, r.Amount, r.DayOfMonth, r.Status, r.NextChargeAt })
                .ToListAsync(ct);
            var messages = await db.Messages
                .Where(m => m.DonorId == id)
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new { m.Id, m.Channel, m.EventType, m.Status, m.Subject, m.CreatedAt, m.SentAt })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                donor = new { donor.Id, donor.Name, donor.Email, donor.Document, donor.Phone },
                donations,
                recurring,
                messages,
            });
        });

        return app;
    }

    private static async Task<HashSet<Guid>> VisibleOrgsAsync(ICurrentUser user, TenantDbContext db, CancellationToken ct)
    {
        if (!user.HasUser) return [];
        var memberIds = await db.OrgMembers.Where(m => m.UserId == user.UserId).Select(m => m.OrganizationId).ToListAsync(ct);
        var all = await db.Organizations.Select(o => new { o.Id, o.ParentId }).ToListAsync(ct);
        return OrgVisibility.VisibleOrgIds(memberIds, all.Select(o => (o.Id, o.ParentId)).ToList());
    }
}

public sealed record CreateOrganizationRequest(string Name, Guid? ParentId = null);

public sealed record AddMemberRequest(Guid? UserId = null, string? Role = null);
