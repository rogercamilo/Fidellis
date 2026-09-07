using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fidellis.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Fábrica design-time do <see cref="CatalogDbContext"/> (usada por <c>dotnet ef migrations</c>).
/// A connection string vem de <c>FIDELLIS_DESIGN_CONN</c> (fallback: Postgres local do compose).
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("FIDELLIS_DESIGN_CONN")
            ?? "Host=localhost;Port=5433;Database=fidellis;Username=fidellis;Password=fidellis_dev";

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(cs, npg => npg.MigrationsHistoryTable("__ef_migrations_history", CatalogDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CatalogDbContext(options);
    }
}
