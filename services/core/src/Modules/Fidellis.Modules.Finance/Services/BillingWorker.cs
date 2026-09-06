using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Finance.Services;

/// <summary>
/// Worker de fundo: a cada intervalo percorre os tenants (schema-per-tenant) e, por tenant, roda
/// dunning → geração de ciclos → reativação de inativos → dispatch da outbox de mensagens.
/// Instância única por ora (lock distribuído via Redis fica para multi-instância). Registrado apenas
/// quando <c>BILLING_ENABLED</c> (desligado em testes/CI).
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
            var sp = scope.ServiceProvider;
            sp.GetRequiredService<ITenantContext>().SetTenant(slug);
            try
            {
                var billing = sp.GetRequiredService<RecurringBillingService>();
                await billing.RunDunningAsync(ct);
                await billing.RunBillingCycleAsync(ct);

                // Expira doações avulsas (PIX/boleto) fora do prazo (RF-FIN-013).
                await sp.GetRequiredService<DonationExpiryService>().ExpireOverdueAsync(ct);

                var reactivationDays = sp.GetRequiredService<InfrastructureOptions>().ReactivationDays;
                await sp.GetRequiredService<ReactivationScanner>().EnqueueInactiveAsync(reactivationDays, ct);

                await sp.GetRequiredService<MessageDispatcher>().DispatchQueuedAsync(ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker do tenant {Slug} falhou.", slug);
            }
        }
    }
}
