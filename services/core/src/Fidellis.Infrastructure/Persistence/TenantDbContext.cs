using Fidellis.Infrastructure.TenantData;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Fidellis.Infrastructure.Persistence;

/// <summary>
/// DbContext dos dados operacionais de UM tenant. O schema (<c>t_&lt;slug&gt;</c>) é resolvido
/// por request via <see cref="ITenantContext"/>. O modelo compilado é cacheado por schema
/// através de <see cref="SchemaModelCacheKeyFactory"/>.
/// </summary>
public sealed class TenantDbContext(
    DbContextOptions<TenantDbContext> options,
    ITenantContext tenantContext) : DbContext(options)
{
    public string Schema => tenantContext.SchemaName
        ?? throw new InvalidOperationException(
            "Nenhum tenant resolvido para o request; TenantDbContext exige um tenant.");

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<Donation> Donations => Set<Donation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Organization>(b =>
        {
            b.ToTable("organizations");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Account>(b =>
        {
            b.ToTable("accounts");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
        });

        modelBuilder.Entity<Transaction>(b =>
        {
            b.ToTable("transactions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.AccountId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AccountingEntry>(b =>
        {
            b.ToTable("accounting_entries");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TransactionId);
            b.Property(x => x.Debit).HasPrecision(18, 2);
            b.Property(x => x.Credit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Donation>(b =>
        {
            b.ToTable("donations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
