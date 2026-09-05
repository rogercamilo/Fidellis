using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Fidellis.Infrastructure.Persistence;

/// <summary>
/// Faz o EF Core cachear um modelo compilado por schema para o <see cref="TenantDbContext"/>.
/// Sem isto, o EF cacheia o primeiro schema visto e o reutiliza para todos os tenants.
/// </summary>
public sealed class SchemaModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is TenantDbContext tenant
            ? (context.GetType(), tenant.Schema, designTime)
            : context.GetType();
}
