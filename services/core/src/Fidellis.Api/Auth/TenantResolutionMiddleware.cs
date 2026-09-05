using System.Text.Json;
using Fidellis.SharedKernel;

namespace Fidellis.Api.Auth;

/// <summary>
/// Resolve o tenant do request e valida o JWT do BFF. Ordem de resolução:
/// 1) claim <c>tenant</c> de um Bearer JWT válido (assinado pelo BFF);
/// 2) header <c>X-Tenant</c> (atalho para dev/testes locais).
/// Popula o <see cref="ITenantContext"/> scoped.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, string jwtSecret)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        string? tenant = null;

        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            var claims = JwtTokenReader.TryValidate(token, jwtSecret);
            if (claims is not null && claims.TryGetValue("tenant", out var t) && t.ValueKind == JsonValueKind.String)
                tenant = t.GetString();
        }

        tenant ??= context.Request.Headers["X-Tenant"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(tenant))
            tenantContext.SetTenant(tenant);

        await next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app, string jwtSecret)
        => app.UseMiddleware<TenantResolutionMiddleware>(jwtSecret);
}
