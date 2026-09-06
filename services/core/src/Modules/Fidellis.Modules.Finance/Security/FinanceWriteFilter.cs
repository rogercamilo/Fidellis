using Fidellis.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Fidellis.Modules.Finance.Security;

/// <summary>
/// Endpoint filter que aplica o RBAC financeiro (RF-FIN-171): em requisições <b>mutantes</b>
/// (POST/PUT/PATCH/DELETE), bloqueia (403) quando o usuário do request tem papel somente-leitura.
/// Leituras (GET) passam livres. Sem usuário/papel, passa (dev/público).
/// </summary>
public sealed class FinanceWriteFilter : IEndpointFilter
{
    private static readonly HashSet<string> Mutating =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (Mutating.Contains(http.Request.Method))
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>();
            if (!FinanceRoles.CanWrite(user.Role))
                return Results.Json(
                    new { error = "Perfil somente-leitura não pode alterar dados financeiros." },
                    statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }
}
