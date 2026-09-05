using Fidellis.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Persistence;

/// <summary>
/// DbContext do schema global <c>catalog</c> — identidade e registro de tenants.
/// Usado pelo módulo Tenant. Schema fixo (não depende do tenant do request).
/// </summary>
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<PspOrder> PspOrders => Set<PspOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Tenant>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Slug).IsUnique();
            b.Property(x => x.Slug).HasMaxLength(63);
            b.Property(x => x.Name).HasMaxLength(200);
            b.Property(x => x.SchemaName).HasMaxLength(63);
        });

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.Email).HasMaxLength(320);
        });

        modelBuilder.Entity<Membership>(b =>
        {
            b.ToTable("memberships");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.TenantId }).IsUnique();
        });

        modelBuilder.Entity<PspOrder>(b =>
        {
            b.ToTable("psp_orders");
            b.HasKey(x => x.ProviderOrderId);
            b.Property(x => x.ProviderOrderId).HasMaxLength(100);
            b.Property(x => x.TenantSlug).HasMaxLength(63);
        });
    }
}
