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
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<FinanceSettings> FinanceSettings => Set<FinanceSettings>();
    public DbSet<DonorType> DonorTypes => Set<DonorType>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<TreasuryAccount> TreasuryAccounts => Set<TreasuryAccount>();
    public DbSet<TreasuryMovement> TreasuryMovements => Set<TreasuryMovement>();
    public DbSet<Receivable> Receivables => Set<Receivable>();
    public DbSet<Payee> Payees => Set<Payee>();
    public DbSet<Payable> Payables => Set<Payable>();
    public DbSet<PayableAllocation> PayableAllocations => Set<PayableAllocation>();
    public DbSet<OutboxMessage> Messages => Set<OutboxMessage>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

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

        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.ToTable("messages");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.DedupeKey).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.DonorId);
        });

        modelBuilder.Entity<AuditLogEntry>(b =>
        {
            b.ToTable("audit_log");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.CreatedAt);
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

        modelBuilder.Entity<CostCenter>(b =>
        {
            b.ToTable("cost_centers");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Fund>(b =>
        {
            b.ToTable("funds");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("projects");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Code).IsUnique();
            b.Property(x => x.BudgetAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<FinanceSettings>(b =>
        {
            b.ToTable("finance_settings");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DonorType>(b =>
        {
            b.ToTable("donor_types");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<FinanceCategory>(b =>
        {
            b.ToTable("finance_categories");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Kind);
        });

        modelBuilder.Entity<IdempotencyKey>(b =>
        {
            b.ToTable("idempotency_keys");
            b.HasKey(x => x.Key);
        });

        modelBuilder.Entity<TreasuryAccount>(b =>
        {
            b.ToTable("treasury_accounts");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OrganizationId);
            b.Property(x => x.OpeningBalance).HasPrecision(18, 2);
        });

        modelBuilder.Entity<TreasuryMovement>(b =>
        {
            b.ToTable("treasury_movements");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.AccountId, x.OccurredAt });
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Receivable>(b =>
        {
            b.ToTable("receivables");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Status, x.DueDate });
            b.HasIndex(x => x.OrganizationId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.ReceivedAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Payee>(b =>
        {
            b.ToTable("payees");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Payable>(b =>
        {
            b.ToTable("payables");
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.Status, x.DueDate });
            b.HasIndex(x => x.PayeeId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<PayableAllocation>(b =>
        {
            b.ToTable("payable_allocations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.PayableId);
            b.Property(x => x.Amount).HasPrecision(18, 2);
        });
    }
}
