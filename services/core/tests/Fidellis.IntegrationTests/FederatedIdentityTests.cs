using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Donations;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Identidade federada (#80 / ADR-0013): import idempotente por (source, externalId); origem implica membro.</summary>
public class FederatedIdentityTests
{
    private static TenantDbContext TDb(string db)
    {
        var t = new TenantContext();
        t.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, t);
    }

    [Fact]
    public async Task Import_creates_federated_members_as_member_with_origin()
    {
        var db = TDb($"fed_{Guid.NewGuid()}");
        var (created, updated) = await FederatedImport.ApplyAsync(db, "formattio", new[]
        {
            new FederatedMember("fmnd_1", "João", "joao@ex.com"),
            new FederatedMember("fmnd_2", "Maria", null),
        });

        Assert.Equal(2, created);
        Assert.Equal(0, updated);
        var joao = await db.Donors.SingleAsync(d => d.ExternalId == "fmnd_1");
        Assert.Equal("formattio", joao.Source);
        Assert.True(joao.IsMember); // origem federada implica membro (dec.8)
    }

    [Fact]
    public async Task Import_is_idempotent_by_external_id()
    {
        var db = TDb($"fed_{Guid.NewGuid()}");
        await FederatedImport.ApplyAsync(db, "formattio", new[] { new FederatedMember("fmnd_1", "João", "joao@ex.com") });

        // Reimporta o mesmo externalId com nome atualizado → atualiza, não duplica.
        var (created, updated) = await FederatedImport.ApplyAsync(db, "formattio", new[] { new FederatedMember("fmnd_1", "João da Silva", "joao@ex.com") });

        Assert.Equal(0, created);
        Assert.Equal(1, updated);
        var donor = await db.Donors.SingleAsync(d => d.Source == "formattio" && d.ExternalId == "fmnd_1");
        Assert.Equal("João da Silva", donor.Name);
    }

    [Fact]
    public async Task Link_is_by_external_id_not_email()
    {
        var db = TDb($"fed_{Guid.NewGuid()}");
        // Mesmo e-mail, externalIds distintos (o e-mail NÃO é único na origem) → dois doadores distintos.
        var (created, _) = await FederatedImport.ApplyAsync(db, "formattio", new[]
        {
            new FederatedMember("fmnd_1", "João", "familia@ex.com"),
            new FederatedMember("fmnd_2", "Maria", "familia@ex.com"),
        });

        Assert.Equal(2, created);
        Assert.Equal(2, await db.Donors.CountAsync(d => d.Source == "formattio"));
    }
}
