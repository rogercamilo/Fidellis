using Fidellis.Modules.Finance.Security;
using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>RBAC financeiro (Onda 1 inc.1.5): política de papéis + filtro de escrita.</summary>
public class FinanceRbacTests
{
    [Theory]
    [InlineData(null, true)]                       // sem papel (dev/público) → permite
    [InlineData("admin", true)]
    [InlineData("treasurer", true)]
    [InlineData("manager", true)]
    [InlineData("fiscal_council", false)]          // conselho fiscal: somente leitura
    [InlineData("accountant", false)]              // contador: somente leitura
    public void CanWrite_reflects_role(string? role, bool expected)
        => Assert.Equal(expected, FinanceRoles.CanWrite(role));

    private static EndpointFilterInvocationContext ContextFor(string method, string? role)
    {
        var services = new ServiceCollection();
        var user = new CurrentUser();
        if (role is not null) user.SetUser(Guid.NewGuid(), role);
        services.AddScoped<ICurrentUser>(_ => user);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        http.Request.Method = method;
        return EndpointFilterInvocationContext.Create(http);
    }

    [Fact]
    public async Task Filter_blocks_write_for_readonly_role()
    {
        var filter = new FinanceWriteFilter();
        var called = false;
        var result = await filter.InvokeAsync(ContextFor("POST", "fiscal_council"),
            _ => { called = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        Assert.False(called); // a cadeia não prosseguiu
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, status.StatusCode);
    }

    [Fact]
    public async Task Filter_allows_write_for_writer_role()
    {
        var filter = new FinanceWriteFilter();
        var called = false;
        await filter.InvokeAsync(ContextFor("POST", "treasurer"),
            _ => { called = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        Assert.True(called);
    }

    [Fact]
    public async Task Filter_allows_read_for_readonly_role()
    {
        var filter = new FinanceWriteFilter();
        var called = false;
        await filter.InvokeAsync(ContextFor("GET", "fiscal_council"),
            _ => { called = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        Assert.True(called); // GET passa mesmo p/ somente-leitura
    }
}
