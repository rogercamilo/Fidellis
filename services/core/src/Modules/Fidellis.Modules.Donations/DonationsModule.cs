using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Donations;

/// <summary>Skeleton do módulo Donations — campanhas/doações/doadores (lógica no roadmap).</summary>
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

        return app;
    }
}
