using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Security;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Fidellis.Modules.Finance.Campaigns;

/// <summary>
/// Campanhas (D-08): gestão autenticada (lançador — D-02) + progresso/prestação de contas. As rotas
/// públicas de campanha (lista/detalhe) ficam no grupo público do <c>FinanceModule</c>.
/// </summary>
public static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaigns(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/finance/campaigns").WithTags("Finance/Campaigns").AddEndpointFilter<FinanceWriteFilter>();

        g.MapGet("/", async (bool? activeOnly, CampaignService svc, CancellationToken ct) =>
            Results.Ok(await svc.ListAsync(activeOnly ?? false, ct)));

        g.MapGet("/{id:guid}/report", async (Guid id, CampaignService svc, CancellationToken ct) =>
            await svc.ReportAsync(id, ct) is { } r ? Results.Ok(r) : Results.NotFound());

        g.MapPost("/", async (
            CreateCampaignRequest req, CampaignService svc, ITenantContext tenant, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            if (!FinanceRoles.CanLaunch(user.Role))
                return Results.Json(new { error = "Somente coordenador ou admin criam campanhas." }, statusCode: StatusCodes.Status403Forbidden);
            if (req.OrganizationId == Guid.Empty)
                return Results.BadRequest(new { error = "organizationId é obrigatório." });
            try
            {
                var c = await svc.CreateAsync(req.OrganizationId, req.Title, req.Slug, req.GoalAmount, req.Description,
                    req.StartsAt, req.EndsAt, req.FundId, req.ProjectId, ct);
                await audit.RecordAsync("campaign.created", "campaign", c.Id.ToString());
                return Results.Created($"/api/finance/campaigns/{c.Id}", new { id = c.Id, slug = c.Slug });
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        g.MapPatch("/{id:guid}/status", async (
            Guid id, UpdateCampaignStatusRequest req, CampaignService svc, ICurrentUser user, IAuditLog audit, CancellationToken ct) =>
        {
            if (!FinanceRoles.CanLaunch(user.Role))
                return Results.Json(new { error = "Somente coordenador ou admin alteram campanhas." }, statusCode: StatusCodes.Status403Forbidden);
            var c = await svc.SetStatusAsync(id, req.Status, ct);
            if (c is null) return Results.NotFound();
            await audit.RecordAsync("campaign.status_changed", "campaign", $"{id}:{c.Status}");
            return Results.Ok(new { id = c.Id, status = c.Status });
        });

        return app;
    }
}

public sealed record CreateCampaignRequest(
    Guid OrganizationId, string Title, string? Slug = null, decimal? GoalAmount = null, string? Description = null,
    DateTimeOffset? StartsAt = null, DateTimeOffset? EndsAt = null, Guid? FundId = null, Guid? ProjectId = null);

public sealed record UpdateCampaignStatusRequest(string Status);
