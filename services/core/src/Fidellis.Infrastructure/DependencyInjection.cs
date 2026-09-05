using System.Net.Http.Headers;
using System.Text;
using Fidellis.Infrastructure.Payments;
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

        // Contexto de tenant e de usuário por request (definidos pelo middleware da API).
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // Relógio (fake nos testes).
        services.AddSingleton<IClock, SystemClock>();

        services.AddDbContext<CatalogDbContext>(o => o
            .UseNpgsql(options.ConnectionString)
            .UseSnakeCaseNamingConvention());

        services.AddDbContext<TenantDbContext>(o => o
            .UseNpgsql(options.ConnectionString)
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, SchemaModelCacheKeyFactory>());

        services.AddSingleton<ISchemaProvisioner, SchemaProvisioner>();

        // Gateway de pagamento (Pagar.me) como HttpClient tipado com Basic auth (sk como usuário).
        services.AddHttpClient<IPaymentGateway, PagarmePaymentGateway>(client =>
        {
            client.BaseAddress = new Uri(options.PagarmeBaseUrl.TrimEnd('/') + "/");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.PagarmeApiKey}:"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        });

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
