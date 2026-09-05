using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Provisioning;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Fidellis.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra a infraestrutura do core: contexto de tenant, DbContexts (catalog global e
    /// tenant por schema), o provisionador de schemas e (lazy) o Redis.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, InfrastructureOptions options)
    {
        services.AddSingleton(options);

        // Contexto de tenant por request (definido pelo middleware da API).
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddDbContext<CatalogDbContext>(o => o
            .UseNpgsql(options.ConnectionString)
            .UseSnakeCaseNamingConvention());

        services.AddDbContext<TenantDbContext>(o => o
            .UseNpgsql(options.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>());

        services.AddSingleton<ISchemaProvisioner, SchemaProvisioner>();

        // Redis registrado de forma preguiçosa: só conecta quando resolvido (readiness),
        // então build/CI não exigem um Redis no ar.
        if (!string.IsNullOrWhiteSpace(options.RedisConnection))
        {
            services.TryAddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(options.RedisConnection!));
        }

        return services;
    }
}
