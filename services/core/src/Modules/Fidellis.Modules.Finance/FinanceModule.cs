using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Finance;

/// <summary>Skeleton do módulo Finance — orquestração de pagamento/repasse (PSP no roadmap).</summary>
public static class FinanceModule
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapFinanceModule(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/finance/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Finance", tenant = tenant.TenantId, schema = tenant.SchemaName }))
            .WithTags("Finance");
        return app;
    }
}
