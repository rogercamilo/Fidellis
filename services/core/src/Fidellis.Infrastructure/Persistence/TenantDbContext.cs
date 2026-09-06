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
    public DbSet<OrgMember> OrgMembers => Set<OrgMember>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<AccountingEntry> AccountingEntries => Set<AccountingEntry>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Donor> Donors => Set<Donor>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<PspRecipient> PspRecipients => Set<PspRecipient>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<RecurringDonation> RecurringDonations => Set<RecurringDonation>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Organization>(b =>
        {
            b.ToTable("organizations");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<OrgMember>(b =>
        {
            b.ToTable("org_members");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.UserId, x.OrganizationId }).IsUnique();
            b.HasIndex(x => x.OrganizationId);
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
            b.HasIndex(x => x.LedgerAccountId);
            b.Property(x => x.Debit).HasPrecision(18, 2);
            b.Property(x => x.Credit).HasPrecision(18, 2);
        });

        modelBuilder.Entity<LedgerAccount>(b =>
        {
            b.ToTable("ledger_accounts");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Receipt>(b =>
        {
            b.ToTable("receipts");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DonationId).IsUnique();
            b.HasIndex(x => new { x.OrganizationId, x.Number }).IsUnique();
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Donation>(b =>
        {
            b.ToTable("donations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
            b.HasIndex(x => x.PspChargeId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Donor>(b =>
        {
            b.ToTable("donors");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Campaign>(b =>
        {
            b.ToTable("campaigns");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
            b.Property(x => x.GoalAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PspRecipient>(b =>
        {
            b.ToTable("psp_recipients");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
        });

        modelBuilder.Entity<PaymentEvent>(b =>
        {
            b.ToTable("payment_events");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ProviderEventId).IsUnique();
            b.Property(x => x.Payload).HasColumnType("jsonb");
        });

        modelBuilder.Entity<RecurringDonation>(b =>
        {
            b.ToTable("recurring_donations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Status, x.NextChargeAt });
            b.HasIndex(x => x.OrganizationId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
