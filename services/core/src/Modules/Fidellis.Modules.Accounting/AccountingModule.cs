using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Accounting;

/// <summary>Skeleton do módulo Accounting — razão/recibos/prestação de contas (roadmap).</summary>
public static class AccountingModule
{
    public static IServiceCollection AddAccountingModule(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapAccountingModule(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounting/ping", (ITenantContext tenant) =>
            Results.Ok(new { module = "Accounting", tenant = tenant.TenantId, schema = tenant.SchemaName }))
            .WithTags("Accounting");
        return app;
    }
}
