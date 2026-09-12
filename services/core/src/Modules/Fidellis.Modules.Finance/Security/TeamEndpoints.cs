using Fidellis.Infrastructure.Audit;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Security;

/// <summary>
/// Endpoints de equipe/papéis (DT-04): lista os membros do tenant e atribui o papel financeiro
/// (somente admin). A atribuição altera a membership global e é registrada em auditoria.
/// </summary>
public static class TeamEndpoints
{
    public static IEndpointRouteBuilder MapFinanceTeam(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/team").WithTags("Finance/Team").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (ITenantContext tenant, TeamService team, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var tenantId = await team.ResolveTenantIdAsync(tenant.TenantId!, ct);
            if (tenantId == Guid.Empty) return Results.NotFound();
            return Results.Ok(await team.ListAsync(tenantId, ct));
        });

        g.MapGet("/roles", () => Results.Ok(FinanceRoles.All));

        g.MapPut("/{userId:guid}/role", async (
            Guid userId, SetRoleRequest req, ITenantContext tenant, ICurrentUser user, TeamService team, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!string.Equals(user.Role, FinanceRoles.Admin, StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = "Somente admin atribui papéis." }, statusCode: StatusCodes.Status403Forbidden);
            try
            {
                var tenantId = await team.ResolveTenantIdAsync(tenant.TenantId!, ct);
                var membership = await team.SetRoleAsync(tenantId, userId, req.Role, ct);
                if (membership is null) return Results.NotFound();
                await audit.RecordAsync("member.role_changed", "membership", $"{userId}:{membership.Role}");
                return Results.Ok(new { userId, role = membership.Role });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        MapInvitations(g);
        return app;
    }

    /// <summary>
    /// Convites de membro + estado de bootstrap (D-01). Convidar/gerenciar equipe é restrito a
    /// <c>admin</c> + coordenador (Q1); leituras (lista/bootstrap) seguem o RBAC do grupo.
    /// </summary>
    private static void MapInvitations(RouteGroupBuilder g)
    {
        // Lista os convites pendentes do tenant.
        g.MapGet("/invitations", async (ITenantContext tenant, InvitationService inv, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var tenantId = await inv.ResolveTenantIdAsync(tenant.TenantId!, ct);
            if (tenantId == Guid.Empty) return Results.NotFound();
            return Results.Ok(await inv.ListPendingAsync(tenantId, ct));
        });

        // Estado de bootstrap (dirige o banner "monte sua equipe" e o override auditável — D-02/Q4).
        g.MapGet("/bootstrap", async (ITenantContext tenant, InvitationService inv, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var tenantId = await inv.ResolveTenantIdAsync(tenant.TenantId!, ct);
            if (tenantId == Guid.Empty) return Results.NotFound();
            return Results.Ok(await inv.BootstrapStatusAsync(tenantId, ct));
        });

        // Cria um convite (e-mail + papel). E-mail já existente vira membership direto (Q2).
        g.MapPost("/invitations", async (
            InviteRequest req, ITenantContext tenant, ICurrentUser user, InvitationService inv, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanInvite(user.Role))
                return Results.Json(new { error = "Somente admin ou coordenador convidam membros." }, statusCode: StatusCodes.Status403Forbidden);
            var t = await inv.ResolveTenantAsync(tenant.TenantId!, ct);
            if (t is null) return Results.NotFound();
            try
            {
                var result = await inv.CreateAsync(t.Id, t.Name, req.Email, req.Role, user.UserId, ct);
                await audit.RecordAsync(
                    result.Outcome == InviteOutcome.MemberAdded ? "member.added" : "invitation.created",
                    "invitation", $"{req.Email.Trim().ToLowerInvariant()}:{req.Role}");
                return Results.Ok(new { outcome = result.Outcome.ToString(), invitation = result.Invitation });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        // Reenvia um convite pendente (novo token + prazo — Q5).
        g.MapPost("/invitations/{id:guid}/resend", async (
            Guid id, ITenantContext tenant, ICurrentUser user, InvitationService inv, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanInvite(user.Role))
                return Results.Json(new { error = "Somente admin ou coordenador gerenciam convites." }, statusCode: StatusCodes.Status403Forbidden);
            var t = await inv.ResolveTenantAsync(tenant.TenantId!, ct);
            if (t is null) return Results.NotFound();
            var dto = await inv.ResendAsync(t.Id, id, t.Name, ct);
            if (dto is null) return Results.NotFound();
            await audit.RecordAsync("invitation.resent", "invitation", id.ToString());
            return Results.Ok(dto);
        });

        // Revoga um convite pendente.
        g.MapDelete("/invitations/{id:guid}", async (
            Guid id, ITenantContext tenant, ICurrentUser user, InvitationService inv, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanInvite(user.Role))
                return Results.Json(new { error = "Somente admin ou coordenador gerenciam convites." }, statusCode: StatusCodes.Status403Forbidden);
            var tenantId = await inv.ResolveTenantIdAsync(tenant.TenantId!, ct);
            if (!await inv.RevokeAsync(tenantId, id, ct)) return Results.NotFound();
            await audit.RecordAsync("invitation.revoked", "invitation", id.ToString());
            return Results.Ok(new { id, status = "revoked" });
        });

        // Conclui/pula a etapa 2 do onboarding (marca a flag; a saída do bootstrap segue derivada).
        g.MapPost("/onboarding/complete", async (
            ITenantContext tenant, ICurrentUser user, InvitationService inv, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanInvite(user.Role))
                return Results.Json(new { error = "Somente admin ou coordenador concluem o onboarding." }, statusCode: StatusCodes.Status403Forbidden);
            var tenantId = await inv.ResolveTenantIdAsync(tenant.TenantId!, ct);
            if (tenantId == Guid.Empty) return Results.NotFound();
            var status = await inv.CompleteOnboardingAsync(tenantId, ct);
            await audit.RecordAsync("onboarding.completed", "tenant", tenant.TenantId);
            return Results.Ok(status);
        });
    }
}

public sealed record SetRoleRequest(string Role);

public sealed record InviteRequest(string Email, string Role);
