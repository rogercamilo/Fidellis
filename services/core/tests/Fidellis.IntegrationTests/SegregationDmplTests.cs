using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Segregação com/sem restrição + DMPL (Onda 4 inc.4.1).</summary>
public class SegregationDmplTests
{
    private static readonly int Year = DateTimeOffset.UtcNow.Year;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    /// <summary>Livre: receita 1000 / despesa 300. Restrito: receita 500 / despesa 100.</summary>
    private static async Task<TenantDbContext> SeededAsync(string db)
    {
        var tdb = TDb(db);
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);

        var free = new Fund { Code = "LIVRE", Name = "Recursos livres", Restriction = "free", IsDefault = true };
        var restricted = new Fund { Code = "OBRA", Name = "Obra social", Restriction = "restricted" };
        tdb.Funds.AddRange(free, restricted);

        void Post(decimal amount, string kind, string ledgerCode, Guid fundId)
        {
            var t = new Transaction { AccountId = Guid.NewGuid(), Kind = kind, Amount = amount, Description = "x", FundId = fundId };
            tdb.Transactions.Add(t);
            var pair = kind == "credit" ? ChartOfAccounts.Receivable : ChartOfAccounts.Bank;
            tdb.AccountingEntries.AddRange(
                new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ledgerCode], Ledger = "x", Debit = kind == "debit" ? amount : 0, Credit = kind == "credit" ? amount : 0 },
                new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[pair], Ledger = "x", Debit = kind == "credit" ? amount : 0, Credit = kind == "debit" ? amount : 0 });
        }

        Post(1000m, "credit", ChartOfAccounts.Revenue, free.Id);
        Post(300m, "debit", ChartOfAccounts.Expense, free.Id);
        Post(500m, "credit", ChartOfAccounts.Revenue, restricted.Id);
        Post(100m, "debit", ChartOfAccounts.Expense, restricted.Id);
        await tdb.SaveChangesAsync();
        return tdb;
    }

    [Fact]
    public async Task Income_is_segregated_by_restriction()
    {
        var tdb = await SeededAsync($"seg_{Guid.NewGuid()}");
        var seg = await new StatementsService(tdb).IncomeSegregatedAsync(Year);

        Assert.Equal(700m, seg.Free.Surplus);        // 1000 - 300
        Assert.Equal(400m, seg.Restricted.Surplus);  // 500 - 100
        Assert.Equal(1100m, seg.Total.Surplus);
    }

    [Fact]
    public async Task Dmpl_closes_with_surplus()
    {
        var tdb = await SeededAsync($"seg_{Guid.NewGuid()}");
        var dmpl = await new StatementsService(tdb).DmplAsync(Year);

        Assert.Equal(0m, dmpl.OpeningEquity);        // sem PL inicial
        Assert.Equal(1100m, dmpl.Surplus);
        Assert.Equal(1100m, dmpl.ClosingEquity);
        Assert.Equal(400m, dmpl.RestrictedSurplus);
    }
}
