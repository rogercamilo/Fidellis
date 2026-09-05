using Fidellis.SharedKernel;
using Xunit;

namespace Fidellis.UnitTests;

public class TenantContextTests
{
    [Fact]
    public void No_tenant_by_default()
    {
        var ctx = new TenantContext();
        Assert.False(ctx.HasTenant);
        Assert.Null(ctx.TenantId);
        Assert.Null(ctx.SchemaName);
    }

    [Fact]
    public void SetTenant_normalizes_and_derives_schema()
    {
        var ctx = new TenantContext();
        ctx.SetTenant("Diocese-SP");

        Assert.True(ctx.HasTenant);
        Assert.Equal("diocese-sp", ctx.TenantId);
        Assert.Equal("t_diocese_sp", ctx.SchemaName);
    }

    [Theory]
    [InlineData("diocese sp", "t_diocese_sp")]
    [InlineData("Paróquia", "t_paróquia")]
    [InlineData("abc123", "t_abc123")]
    public void ToSchemaName_slugifies(string input, string expected)
        => Assert.Equal(expected, TenantContext.ToSchemaName(input));

    [Fact]
    public void SetTenant_rejects_empty()
        => Assert.Throws<ArgumentException>(() => new TenantContext().SetTenant("  "));
}
