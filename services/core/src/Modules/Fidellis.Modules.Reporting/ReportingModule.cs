using Fidellis.Infrastructure.Organizations;
using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Reporting;

/// <summary>
/// Módulo Reporting — dashboards e consolidação da rede (Rede→Unidade via <see cref="OrgVisibility"/>),
/// com base em doações pagas e escopo nas unidades visíveis do usuário.
/// </summary>
public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapReportingModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reporting").WithTags("Reporting");

        group.MapGet("/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Reporting", tenant = tenant.TenantId, schema = tenant.SchemaName }));

        // Resumo consolidado do período.
        group.MapGet("/overview", async (
            ITenantContext tenant, ICurrentUser user, TenantDbContext db,
            DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var paid = await db.Donations
                .Where(d => d.Status == "paid" && visible.Contains(d.OrganizationId)
                    && (fromDate == null || d.PaidAt >= fromDate) && (toDate == null || d.PaidAt <= toDate))
                .Select(d => new { d.Amount, d.Method, d.DonorId })
                .ToListAsync(ct);

            var total = paid.Sum(p => p.Amount);
            var count = paid.Count;
            var activeRecurring = await db.RecurringDonations
                .CountAsync(r => r.Status == "active" && visible.Contains(r.OrganizationId), ct);

            return Results.Ok(new
            {
                from = fromDate,
                to = toDate,
                totalRaised = total,
                donationsCount = count,
                avgTicket = count > 0 ? Math.Round(total / count, 2) : 0m,
                activeDonors = paid.Where(p => p.DonorId != null).Select(p => p.DonorId).Distinct().Count(),
                activeRecurring,
                byMethod = paid.GroupBy(p => p.Method)
                    .Select(g => new { method = g.Key, total = g.Sum(x => x.Amount), count = g.Count() })
                    .OrderByDescending(x => x.total)
                    .ToList(),
            });
        });

        // Série temporal mensal (zero-fill).
        group.MapGet("/timeseries", async (
            ITenantContext tenant, ICurrentUser user, TenantDbContext db, int? months, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var paid = await db.Donations
                .Where(d => d.Status == "paid" && d.PaidAt != null && visible.Contains(d.OrganizationId))
                .Select(d => new { d.PaidAt, d.Amount })
                .ToListAsync(ct);

            var series = ReportingCalc.MonthlySeries(
                paid.Select(p => (p.PaidAt!.Value, p.Amount)), months ?? 12, DateTimeOffset.UtcNow);
            return Results.Ok(series);
        });

        // Consolidação por unidade (Rede→Unidade).
        group.MapGet("/by-unit", async (
            ITenantContext tenant, ICurrentUser user, TenantDbContext db,
            DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var visible = await VisibleOrgsAsync(user, db, ct);

            var orgs = await db.Organizations
                .Where(o => visible.Contains(o.Id))
                .Select(o => new { o.Id, o.Name, o.ParentId })
                .ToListAsync(ct);

            var paid = await db.Donations
                .Where(d => d.Status == "paid" && visible.Contains(d.OrganizationId)
                    && (fromDate == null || d.PaidAt >= fromDate) && (toDate == null || d.PaidAt <= toDate))
                .Select(d => new { d.OrganizationId, d.Amount })
                .ToListAsync(ct);

            var byOrg = paid.GroupBy(p => p.OrganizationId)
                .ToDictionary(g => g.Key, g => (Total: g.Sum(x => x.Amount), Count: g.Count()));

            var result = orgs
                .Select(o =>
                {
                    var agg = byOrg.GetValueOrDefault(o.Id);
                    return new { organizationId = o.Id, name = o.Name, parentId = o.ParentId, total = agg.Total, count = agg.Count };
                })
                .OrderByDescending(x => x.total)
                .ToList();

            return Results.Ok(result);
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
