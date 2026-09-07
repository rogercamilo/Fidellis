using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Snapshot/assinatura das demonstrações (DT-10): congela o rascunho e gerencia a aprovação.</summary>
public class StatementSnapshotTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private const int Year = 2025;
    private static readonly DateOnly D = new(Year, 6, 15);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static async Task<StatementSnapshotService> SvcAsync(string db)
    {
        var tdb = TDb(db);
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);
        var t = new Transaction { AccountId = Guid.NewGuid(), Kind = "credit", Amount = 1000m, Description = "Doação", AccountingDate = D };
        tdb.Transactions.Add(t);
        tdb.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "R", Debit = 1000m, Credit = 0, AccountingDate = D },
            new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "R", Debit = 0, Credit = 1000m, AccountingDate = D });
        await tdb.SaveChangesAsync();
        return new StatementSnapshotService(tdb, new StatementsService(tdb), new FixedClock(new DateTimeOffset(Year, 7, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task Generate_freezes_a_draft_with_hash_and_payload()
    {
        var svc = await SvcAsync($"snap_{Guid.NewGuid()}");
        var s = await svc.GenerateAsync(Year, "userA");

        Assert.Equal("draft", s.Status);
        Assert.Equal(64, s.Hash.Length);          // SHA-256 hex
        Assert.Contains("income", s.Payload);
        Assert.Contains("balanceSheet", s.Payload);
    }

    [Fact]
    public async Task Approve_signs_the_snapshot()
    {
        var svc = await SvcAsync($"snap_{Guid.NewGuid()}");
        var s = await svc.GenerateAsync(Year, "userA");

        var approved = await svc.ApproveAsync(s.Id, "userB");
        Assert.Equal("approved", approved!.Status);
        Assert.Equal("userB", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
    }

    [Fact]
    public async Task Approver_cannot_be_the_generator()
    {
        var svc = await SvcAsync($"snap_{Guid.NewGuid()}");
        var s = await svc.GenerateAsync(Year, "userA");

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ApproveAsync(s.Id, "userA")); // segregação
    }

    [Fact]
    public async Task Approve_is_idempotent()
    {
        var svc = await SvcAsync($"snap_{Guid.NewGuid()}");
        var s = await svc.GenerateAsync(Year, "userA");
        var first = await svc.ApproveAsync(s.Id, "userB");
        var second = await svc.ApproveAsync(s.Id, "userC"); // já aprovado → mantém

        Assert.Equal("userB", second!.ApprovedBy);
        Assert.Equal(first!.ApprovedAt, second.ApprovedAt);
    }
}
