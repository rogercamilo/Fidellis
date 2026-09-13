using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Credencial de serviço da integração federada (#80 / ADR-0013 dec.9): emissão/validação por origem.</summary>
public class IntegrationCredentialTests
{
    private static TenantDbContext TDb(string db)
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);
    }

    [Fact]
    public async Task Issued_key_validates_and_wrong_key_is_rejected()
    {
        var svc = new IntegrationCredentialService(TDb($"ic_{Guid.NewGuid()}"));
        var key = await svc.IssueAsync("formattio");

        Assert.True(await svc.ValidateAsync("formattio", key));
        Assert.False(await svc.ValidateAsync("formattio", "chave-errada"));
        Assert.False(await svc.ValidateAsync("formattio", null));
    }

    [Fact]
    public async Task Rotating_the_key_invalidates_the_previous_one()
    {
        var svc = new IntegrationCredentialService(TDb($"ic_{Guid.NewGuid()}"));
        var k1 = await svc.IssueAsync("formattio");
        var k2 = await svc.IssueAsync("formattio"); // rotaciona

        Assert.NotEqual(k1, k2);
        Assert.False(await svc.ValidateAsync("formattio", k1));
        Assert.True(await svc.ValidateAsync("formattio", k2));
    }

    [Fact]
    public async Task Status_reflects_configuration_and_hash_is_never_the_plaintext()
    {
        var db = TDb($"ic_{Guid.NewGuid()}");
        var svc = new IntegrationCredentialService(db);
        Assert.Equal((false, false), await svc.StatusAsync("formattio"));

        var key = await svc.IssueAsync("formattio");
        Assert.Equal((true, true), await svc.StatusAsync("formattio"));
        var stored = await db.IntegrationCredentials.SingleAsync();
        Assert.NotEqual(key, stored.KeyHash); // guarda só o hash, nunca o valor em claro
    }
}
