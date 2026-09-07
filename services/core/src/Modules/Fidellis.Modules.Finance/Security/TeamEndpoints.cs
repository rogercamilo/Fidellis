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

        return app;
    }
}

public sealed record SetRoleRequest(string Role);
