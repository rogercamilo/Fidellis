using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>
/// Testes que sobem a API in-memory (sem Postgres). Cobrem liveness e a resolução de tenant,
/// que não dependem de banco — o bootstrap do catalog é best-effort no startup.
/// </summary>
public class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory = factory;

    [Fact]
    public async Task Liveness_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("live", body);
    }

    [Fact]
    public async Task Root_returns_service_name()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fidellis.Api", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Module_ping_without_tenant_is_rejected()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/donations/ping");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
