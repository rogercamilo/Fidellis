using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Audit;

/// <summary>Módulo Audit — trilha de auditoria (quem fez o quê) do tenant.</summary>
public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAuditModule(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").WithTags("Audit");

        group.MapGet("/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Audit", tenant = tenant.TenantId, schema = tenant.SchemaName }));

        group.MapGet("/log", async (ITenantContext tenant, TenantDbContext db, CancellationToken ct) =>
        {
            if (!tenant.HasTenant) return Results.BadRequest(new { error = "Nenhum tenant no request." });
            var list = await db.AuditLog
                .OrderByDescending(a => a.CreatedAt)
                .Take(200)
                .Select(a => new { a.Id, a.ActorUserId, a.Action, a.Entity, a.EntityId, a.CreatedAt })
                .ToListAsync(ct);
            return Results.Ok(list);
        });

        return app;
    }
}
