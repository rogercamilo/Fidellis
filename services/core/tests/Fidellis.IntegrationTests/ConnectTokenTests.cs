using Fidellis.Infrastructure.Security;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Código de pareamento (#80 / P1b): HMAC sem estado — assina/valida (organizationId, tenant, exp).</summary>
public class ConnectTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Round_trips_and_rejects_tampering()
    {
        var org = Guid.NewGuid();
        var token = ConnectToken.Sign(org, "diocese-sp", Now.AddMinutes(10), "secret");

        var ok = ConnectToken.Validate(token, "secret", Now);
        Assert.NotNull(ok);
        Assert.Equal(org, ok!.Value.OrganizationId);
        Assert.Equal("diocese-sp", ok.Value.Tenant);

        Assert.Null(ConnectToken.Validate(token, "outro-segredo", Now));      // segredo errado
        Assert.Null(ConnectToken.Validate(token, "secret", Now.AddMinutes(11))); // expirado
        Assert.Null(ConnectToken.Validate("lixo", "secret", Now));            // formato inválido
    }
}
