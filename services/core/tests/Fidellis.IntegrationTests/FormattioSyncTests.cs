using System.Net;
using System.Text;
using Fidellis.Infrastructure;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Donations;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Puller da integração federada (#80, Opção A): puxa do Formattio e faz upsert dos ativos.</summary>
public class FormattioSyncTests
{
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
    }

    private static TenantDbContext TDb(string db)
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);
    }

    private static InfrastructureOptions Options(bool configured) => new()
    {
        ConnectionString = "x",
        FormattioBaseUrl = configured ? "https://formattio.test" : null,
        FormattioPullSecret = configured ? "secret" : null,
    };

    [Fact]
    public async Task Sync_upserts_active_members_and_skips_inactive()
    {
        const string json = """
        {"organizacaoId":"org1","members":[
          {"externalId":"f1","name":"João","email":"j@x.com","active":true},
          {"externalId":"f2","name":"Maria","email":null,"active":false}
        ]}
        """;
        var db = TDb($"sync_{Guid.NewGuid()}");
        var svc = new FormattioSyncService(new HttpClient(new StubHandler(json)), db, Options(true), NullLogger<FormattioSyncService>.Instance);

        var r = await svc.SyncAsync("org1");

        Assert.Equal(1, r.Created);
        Assert.Equal(0, r.Updated);
        Assert.Equal(2, r.Total);
        Assert.Equal(1, r.SkippedInactive);
        var f1 = await db.Donors.SingleAsync(d => d.ExternalId == "f1");
        Assert.True(f1.IsMember);              // ativo importado como membro
        Assert.Equal("formattio", f1.Source);
        Assert.Equal(0, await db.Donors.CountAsync(d => d.ExternalId == "f2")); // inativo não importado
    }

    [Fact]
    public async Task Sync_throws_when_not_configured()
    {
        var svc = new FormattioSyncService(new HttpClient(new StubHandler("{}")), TDb($"s_{Guid.NewGuid()}"), Options(false), NullLogger<FormattioSyncService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SyncAsync("org1"));
    }
}
