using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>
/// Rate limiting dos endpoints públicos (RF-FIN-002 / DT-11): sobe a API in-memory e prova, de ponta a
/// ponta, que a janela fixa admite exatamente <c>PUBLIC_RATE_LIMIT_PERMITS</c> requisições por
/// IP+tenant e rejeita as seguintes com 429. Independe de Postgres — o limiter roda antes do endpoint.
/// </summary>
public class RateLimitEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const int Permits = 3;

    private readonly WebApplicationFactory<Program> _factory = factory.WithWebHostBuilder(b =>
    {
        b.UseSetting("BILLING_ENABLED", "false");
        b.UseSetting("PUBLIC_RATE_LIMIT_PERMITS", Permits.ToString());
        b.UseSetting("PUBLIC_RATE_LIMIT_WINDOW_SECONDS", "300"); // janela larga: não reseta durante o teste
    });

    [Fact]
    public async Task Public_endpoint_allows_permits_then_returns_429()
    {
        var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < Permits + 2; i++)
        {
            var res = await client.GetAsync("/api/public/demo/transparency?year=2025");
            statuses.Add(res.StatusCode);
        }

        // As primeiras `Permits` são admitidas (não 429; o resultado do endpoint em si é irrelevante aqui).
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(Permits));
        // As excedentes são barradas com 429 pelo limiter, antes de chegar ao endpoint.
        Assert.All(statuses.Skip(Permits), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }

    [Fact]
    public async Task Rejection_includes_retry_after_header()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? rejected = null;
        for (var i = 0; i < Permits + 1; i++)
            rejected = await client.GetAsync("/api/public/demo/transparency?year=2025");

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected!.StatusCode);
        Assert.True(rejected.Headers.RetryAfter is not null); // OnRejected seta Retry-After
    }
}
