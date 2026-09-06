using System.Text.Json;
using Fidellis.SharedKernel;

namespace Fidellis.Api.Auth;

/// <summary>
/// Resolve o tenant e o usuário do request e valida o JWT do BFF. Ordem de resolução:
/// 1) claims <c>tenant</c>/<c>sub</c> de um Bearer JWT válido (assinado pelo BFF);
/// 2) headers <c>X-Tenant</c>/<c>X-User</c> (atalho para dev/testes locais).
/// Popula o <see cref="ITenantContext"/> e o <see cref="ICurrentUser"/> scoped.
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, string jwtSecret)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, ICurrentUser currentUser)
    {
        string? tenant = null;
        string? userId = null;
        string? role = null;

        var auth = context.Request.Headers.Authorization.ToString();
        if (auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = auth["Bearer ".Length..].Trim();
            var claims = JwtTokenReader.TryValidate(token, jwtSecret);
            if (claims is not null)
            {
                if (claims.TryGetValue("tenant", out var t) && t.ValueKind == JsonValueKind.String)
                    tenant = t.GetString();
                if (claims.TryGetValue("sub", out var s) && s.ValueKind == JsonValueKind.String)
                    userId = s.GetString();
                if (claims.TryGetValue("role", out var r) && r.ValueKind == JsonValueKind.String)
                    role = r.GetString();
            }
        }

        tenant ??= context.Request.Headers["X-Tenant"].FirstOrDefault();
        userId ??= context.Request.Headers["X-User"].FirstOrDefault();
        role ??= context.Request.Headers["X-Role"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(tenant))
            tenantContext.SetTenant(tenant);
        if (Guid.TryParse(userId, out var uid))
            currentUser.SetUser(uid, role);

        await next(context);
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app, string jwtSecret)
        => app.UseMiddleware<TenantResolutionMiddleware>(jwtSecret);
}
