using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Worker de billing: a cada intervalo percorre os tenants (schema-per-tenant), e para cada um roda
/// o dunning e depois a geração de ciclos. Instância única por ora (lock distribuído via Redis fica
/// para multi-instância). Registrado apenas quando <c>BILLING_ENABLED</c> (desligado em testes/CI).
/// </summary>
public sealed class BillingWorker(
    IServiceScopeFactory scopeFactory,
    BillingOptions options,
    ILogger<BillingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        logger.LogInformation("BillingWorker iniciado (intervalo {Interval}s).", interval.TotalSeconds);

        try
        {
            do
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (Exception ex) { logger.LogError(ex, "Passada de billing falhou."); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        List<string> slugs;
        using (var scope = scopeFactory.CreateScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            slugs = await catalog.Tenants.Select(t => t.Slug).ToListAsync(ct);
        }

        foreach (var slug in slugs)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(slug);
            var billing = scope.ServiceProvider.GetRequiredService<RecurringBillingService>();
            try
            {
                await billing.RunDunningAsync(ct);
                await billing.RunBillingCycleAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Billing do tenant {Slug} falhou.", slug);
            }
        }
    }
}
