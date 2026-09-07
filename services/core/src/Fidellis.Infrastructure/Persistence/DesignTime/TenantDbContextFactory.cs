using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fidellis.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Fábrica design-time do <see cref="TenantDbContext"/> (usada por <c>dotnet ef migrations</c>).
/// O modelo é schemaless (DT-05), então nenhum tenant precisa ser resolvido para gerar as migrações.
/// A connection string vem de <c>FIDELLIS_DESIGN_CONN</c> (fallback: Postgres local do compose).
/// </summary>
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("FIDELLIS_DESIGN_CONN")
            ?? "Host=localhost;Port=5433;Database=fidellis;Username=fidellis;Password=fidellis_dev";

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TenantDbContext(options, new TenantContext());
    }
}
