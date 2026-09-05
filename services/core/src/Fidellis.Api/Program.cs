using Fidellis.Api.Auth;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Provisioning;
using Fidellis.Modules.Accounting;
using Fidellis.Modules.Audit;
using Fidellis.Modules.Donations;
using Fidellis.Modules.Finance;
using Fidellis.Modules.Finance.Services;
using Fidellis.Modules.Reporting;
using Fidellis.Modules.Tenant;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;

var connectionString = config["DATABASE_URL"]
    ?? "Host=localhost;Port=5432;Database=fidellis;Username=fidellis;Password=fidellis_dev";
var redisConnection = NormalizeRedis(config["REDIS_URL"]);
var jwtSecret = config["JWT_SECRET"] ?? "change-me-in-prod-please-use-a-long-random-secret";
var webOrigin = config["NEXT_PUBLIC_BFF_URL"] ?? "http://localhost:4000";

builder.Services.AddInfrastructure(new InfrastructureOptions
{
    ConnectionString = connectionString,
    RedisConnection = redisConnection,
    PagarmeApiKey = config["PAGARME_API_KEY"],
    PagarmeBaseUrl = config["PAGARME_BASE_URL"] ?? "https://api.pagar.me/core/v5",
    PagarmeWebhookUser = config["PAGARME_WEBHOOK_USER"],
    PagarmeWebhookPassword = config["PAGARME_WEBHOOK_PASSWORD"],
});

builder.Services
    .AddTenantModule()
    .AddDonationsModule()
    .AddFinanceModule()
    .AddAccountingModule()
    .AddReportingModule()
    .AddAuditModule();

// Motor de recorrência/dunning: opções do ambiente + worker (desligado em testes/CI via BILLING_ENABLED=false).
var billingOptions = new BillingOptions
{
    Enabled = config["BILLING_ENABLED"] is not "false",
    IntervalSeconds = int.TryParse(config["BILLING_INTERVAL_SECONDS"], out var iv) ? iv : 300,
    DunningDays = ParseDunning(config["BILLING_DUNNING_DAYS"]),
    CycleExpirySeconds = int.TryParse(config["BILLING_CYCLE_EXPIRY_SECONDS"], out var ce) ? ce : 86400,
};
builder.Services.AddSingleton(billingOptions);
if (billingOptions.Enabled)
    builder.Services.AddHostedService<BillingWorker>();

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(webOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Bootstrap do schema catalog (best-effort: se o Postgres não estiver no ar, o app ainda
// sobe e /health/live responde — útil para CI e primeira execução).
await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = app.Logger;
    try
    {
        var provisioner = scope.ServiceProvider.GetRequiredService<ISchemaProvisioner>();
        await provisioner.EnsureCatalogAsync();
        await provisioner.EnsureAllTenantsAsync();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Não foi possível garantir o schema catalog no startup (Postgres indisponível?).");
    }
}

app.UseCors();
app.UseTenantResolution(jwtSecret);

// Liveness: o processo está de pé (não toca dependências externas).
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).WithTags("Health");

// Readiness: dependências externas (Postgres, Redis) respondem.
app.MapGet("/health/ready", async (IServiceProvider sp, CancellationToken ct) =>
{
    var checks = new Dictionary<string, string>();
    var healthy = true;

    try
    {
        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        checks["postgres"] = "ok";
    }
    catch
    {
        checks["postgres"] = "down";
        healthy = false;
    }

    var mux = sp.GetService<IConnectionMultiplexer>();
    if (mux is not null)
    {
        try
        {
            await mux.GetDatabase().PingAsync();
            checks["redis"] = "ok";
        }
        catch
        {
            checks["redis"] = "down";
            healthy = false;
        }
    }

    return healthy
        ? Results.Ok(new { status = "ready", checks })
        : Results.Json(new { status = "unready", checks }, statusCode: 503);
}).WithTags("Health");

app.MapGet("/", () => Results.Ok(new { service = "Fidellis.Api", status = "ok" }));

app.MapTenantModule();
app.MapDonationsModule();
app.MapFinanceModule();
app.MapAccountingModule();
app.MapReportingModule();
app.MapAuditModule();

app.Run();

// Converte REDIS_URL (redis://host:port) para o formato do StackExchange (host:port).
static string? NormalizeRedis(string? url)
{
    if (string.IsNullOrWhiteSpace(url)) return null;
    return url.StartsWith("redis://", StringComparison.OrdinalIgnoreCase)
        ? url["redis://".Length..].TrimEnd('/')
        : url;
}

// Parseia "1,3,5" -> [1,3,5]; vazio/ inválido usa a agenda padrão.
static int[] ParseDunning(string? csv)
{
    if (string.IsNullOrWhiteSpace(csv)) return [1, 3, 5];
    var days = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => int.TryParse(s, out var d) ? d : -1)
        .Where(d => d > 0)
        .ToArray();
    return days.Length > 0 ? days : [1, 3, 5];
}

// Exposto para o WebApplicationFactory nos testes de integração.
public partial class Program;
