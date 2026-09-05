using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Audit;

/// <summary>Skeleton do módulo Audit — trilha de auditoria/LGPD (roadmap).</summary>
public static class AuditModule
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAuditModule(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Audit", tenant = tenant.TenantId, schema = tenant.SchemaName }))
            .WithTags("Audit");
        return app;
    }
}
