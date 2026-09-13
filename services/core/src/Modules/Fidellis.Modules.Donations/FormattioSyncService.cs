using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Fidellis.Modules.Donations;

public sealed record FormattioSyncResult(int Created, int Updated, int Total, int SkippedInactive);

/// <summary>
/// Puller da integração federada (#80, Opção A — "Fidellis puxa"). Chama o endpoint de sincronização do
/// Formattio (server-to-server, Bearer <c>FORMATTIO_PULL_SECRET</c>) e faz o upsert dos membros ativos via
/// <see cref="FederatedImport"/>. Minimização: só id+origem+nome+e-mail. Membros inativos são ignorados no
/// import (o offboarding é tratado pelo sinal push do Formattio / endpoint de offboard). Roda no schema do tenant.
/// </summary>
public sealed class FormattioSyncService(
    HttpClient http, TenantDbContext db, InfrastructureOptions options, ILogger<FormattioSyncService> logger)
{
    public bool Configured =>
        !string.IsNullOrWhiteSpace(options.FormattioBaseUrl) && !string.IsNullOrWhiteSpace(options.FormattioPullSecret);

    public async Task<FormattioSyncResult> SyncAsync(string organizacaoId, CancellationToken ct = default)
    {
        if (!Configured)
            throw new InvalidOperationException("Integração Formattio não configurada (FORMATTIO_BASE_URL / FORMATTIO_PULL_SECRET).");

        var baseUrl = options.FormattioBaseUrl!.TrimEnd('/');
        var url = $"{baseUrl}/api/integrations/fidellis/formandos?organizacaoId={Uri.EscapeDataString(organizacaoId)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.FormattioPullSecret);

        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        var payload = await resp.Content.ReadFromJsonAsync<PullPayload>(ct) ?? new PullPayload();
        var all = payload.Members ?? [];

        var active = all
            .Where(m => m.Active && !string.IsNullOrWhiteSpace(m.ExternalId) && !string.IsNullOrWhiteSpace(m.Name))
            .Select(m => new FederatedMember(m.ExternalId!, m.Name!, m.Email))
            .ToList();

        var (created, updated) = await FederatedImport.ApplyAsync(db, "formattio", active, ct);
        var skipped = all.Count - active.Count;
        logger.LogInformation(
            "Formattio sync org {Org}: {Created} criados, {Updated} atualizados, {Skipped} inativos ignorados.",
            organizacaoId, created, updated, skipped);
        return new FormattioSyncResult(created, updated, all.Count, skipped);
    }

    private sealed class PullPayload { public List<PullMember>? Members { get; set; } }
    private sealed class PullMember
    {
        public string? ExternalId { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public bool Active { get; set; }
    }
}
