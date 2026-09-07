using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Portal de transparência (Onda 4 inc.4.4): resumo público trimestral/anual.</summary>
public class TransparencyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly int Year = Now.Year;
    private static readonly int CurrentQuarter = (Now.Month - 1) / 3 + 1;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static async Task<TenantDbContext> SeededAsync(string db)
    {
        var tdb = TDb(db);
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);

        var tRev = new Transaction { AccountId = Guid.NewGuid(), Kind = "credit", Amount = 1000m, Description = "Doação" };
        var tExp = new Transaction { AccountId = Guid.NewGuid(), Kind = "debit", Amount = 300m, Description = "Despesa" };
        tdb.Transactions.AddRange(tRev, tExp);
        tdb.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "R", Debit = 1000m, Credit = 0 },
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "R", Debit = 0, Credit = 1000m },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Expense], Ledger = "D", Debit = 300m, Credit = 0 },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Bank], Ledger = "B", Debit = 0, Credit = 300m });
        await tdb.SaveChangesAsync();
        return tdb;
    }

    [Fact]
    public async Task Yearly_summary_reports_result_and_balance()
    {
        var tdb = await SeededAsync($"tr_{Guid.NewGuid()}");
        var s = await new StatementsService(tdb).TransparencyAsync(Year, null);
        Assert.Equal(1000m, s.Revenues);
        Assert.Equal(300m, s.Expenses);
        Assert.Equal(700m, s.Surplus);
        Assert.Equal(700m, s.Assets);      // Recebível 1000 - Banco 300
        Assert.Equal(700m, s.NetEquity);   // PL + superávit acumulado
    }

    [Fact]
    public async Task Current_quarter_includes_this_period_movements()
    {
        var tdb = await SeededAsync($"tr_{Guid.NewGuid()}");
        var s = await new StatementsService(tdb).TransparencyAsync(Year, CurrentQuarter);
        Assert.Equal(700m, s.Surplus); // lançamentos de agora caem no trimestre corrente
    }

    [Fact]
    public async Task Future_quarter_has_no_period_result_yet()
    {
        var tdb = await SeededAsync($"tr_{Guid.NewGuid()}");
        var next = CurrentQuarter == 4 ? (Year + 1, 1) : (Year, CurrentQuarter + 1);
        var s = await new StatementsService(tdb).TransparencyAsync(next.Item1, next.Item2);
        Assert.Equal(0m, s.Surplus); // nada lançado no trimestre futuro
    }
}
