using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Reporting;

/// <summary>Skeleton do módulo Reporting — dashboards/exportações/consolidação da rede (roadmap).</summary>
public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapReportingModule(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reporting/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Reporting", tenant = tenant.TenantId, schema = tenant.SchemaName }))
            .WithTags("Reporting");
        return app;
    }
}
