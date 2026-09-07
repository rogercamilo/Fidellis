using System.Net.Http.Headers;
using System.Text;
using Fidellis.Infrastructure.Audit;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.Provisioning;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
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
            .UseNpgsql(options.ConnectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention());

        // Interceptor que aponta o search_path da conexão para o schema do tenant (DT-05).
        services.AddScoped<TenantSearchPathInterceptor>();

        // optionsLifetime scoped: o interceptor (scoped) é resolvido do provider do request, com o
        // ITenantContext correto — senão o options singleton capturaria um tenant nulo.
        services.AddDbContext<TenantDbContext>((sp, o) => o
            // Histórico sem schema explícito: resolve no schema do tenant via search_path.
            .UseNpgsql(options.ConnectionString, npg => npg
                .MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<TenantSearchPathInterceptor>()),
            optionsLifetime: ServiceLifetime.Scoped);

        // Provisionamento de schema: migrações EF versionadas (padrão) ou DDL idempotente (fallback).
        if (string.Equals(options.SchemaStrategy, "ddl", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<ISchemaProvisioner, SchemaProvisioner>();
        else
            services.AddSingleton<ISchemaProvisioner, EfSchemaProvisioner>();

        // Contabilidade: plano de contas + recibos (usados pela conciliação e pelo módulo Accounting).
        services.AddScoped<Accounting.ChartOfAccountsSeeder>();
        services.AddScoped<Accounting.ReceiptService>();
        services.AddSingleton<Accounting.ReceiptPdfService>();

        // Storage de arquivos (recibos em PDF): S3/R2 quando configurado, senão no-op (gera sob demanda).
        if (!string.IsNullOrWhiteSpace(options.StorageEndpoint) && !string.IsNullOrWhiteSpace(options.StorageBucket))
            services.AddSingleton<Storage.IObjectStorage, Storage.S3ObjectStorage>();
        else
            services.AddSingleton<Storage.IObjectStorage, Storage.NullObjectStorage>();

        // Dimensões gerenciais (centros de custo/fundos/projetos) + seeding dos defaults.
        services.AddScoped<Dimensions.DimensionsSeeder>();

        // Configuração financeira (nomenclaturas + tipos de doador) + seeding dos defaults.
        services.AddScoped<Configuration.FinanceConfigSeeder>();

        // Auditoria (trilha).
        services.AddScoped<IAuditLog, AuditLog>();

        // Gateway de pagamento (Pagar.me) como HttpClient tipado com Basic auth (sk como usuário).
        services.AddHttpClient<IPaymentGateway, PagarmePaymentGateway>(client =>
        {
            client.BaseAddress = new Uri(options.PagarmeBaseUrl.TrimEnd('/') + "/");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.PagarmeApiKey}:"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        });

        // Mensageria (régua de relacionamento): outbox + senders + dispatcher.
        services.AddScoped<MessageOutbox>();
        services.AddScoped<MessageDispatcher>();
        services.AddScoped<ReactivationScanner>();
        services.AddScoped<WhatsAppSender>();
        services.AddScoped<IMessageSender>(sp => sp.GetRequiredService<WhatsAppSender>());
        services.AddHttpClient<ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            if (!string.IsNullOrWhiteSpace(options.ResendApiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ResendApiKey);
        });
        services.AddScoped<IMessageSender>(sp => sp.GetRequiredService<ResendEmailSender>());

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
