using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    private bool EnvConfigured =>
        !string.IsNullOrWhiteSpace(options.FormattioBaseUrl) && !string.IsNullOrWhiteSpace(options.FormattioPullSecret);

    /// <summary>Há config utilizável — conexão persistida (P1b) ou env (fallback).</summary>
    public async Task<bool> HasConfigAsync(CancellationToken ct = default)
        => EnvConfigured || await db.IntegrationConnections.AnyAsync(c => c.Source == "formattio" && c.Enabled, ct);

    /// <summary>
    /// Sincroniza a partir da <b>conexão persistida</b> (P1b) ou, na ausência, dos <b>env</b> (fallback).
    /// <paramref name="organizacaoIdOverride"/> permite forçar a organização (obrigatório no modo env).
    /// </summary>
    public async Task<FormattioSyncResult> SyncAsync(string? organizacaoIdOverride = null, CancellationToken ct = default)
    {
        var conn = await db.IntegrationConnections.FirstOrDefaultAsync(c => c.Source == "formattio" && c.Enabled, ct);

        string baseUrl, secret, orgId;
        if (conn is not null)
        {
            baseUrl = conn.ExternalBaseUrl;
            secret = conn.PullSecret;
            orgId = string.IsNullOrWhiteSpace(organizacaoIdOverride) ? conn.ExternalOrgId : organizacaoIdOverride!;
        }
        else if (EnvConfigured)
        {
            baseUrl = options.FormattioBaseUrl!;
            secret = options.FormattioPullSecret!;
            orgId = organizacaoIdOverride
                ?? throw new InvalidOperationException("organizacaoId é obrigatório no modo env (sem conexão estabelecida).");
        }
        else
        {
            throw new InvalidOperationException("Integração Formattio não configurada (conecte via código de pareamento ou defina FORMATTIO_BASE_URL/FORMATTIO_PULL_SECRET).");
        }

        var url = $"{baseUrl.TrimEnd('/')}/api/integrations/fidellis/formandos?organizacaoId={Uri.EscapeDataString(orgId)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);

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

        if (conn is not null)
        {
            conn.LastSyncAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Formattio sync org {Org}: {Created} criados, {Updated} atualizados, {Skipped} inativos ignorados.",
            orgId, created, updated, skipped);
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
