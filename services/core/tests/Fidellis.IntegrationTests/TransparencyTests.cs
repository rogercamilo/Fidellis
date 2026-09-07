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
    // Competência fixa (DT-07/DT-13): período histórico determinístico.
    private const int Year = 2025;
    private static readonly DateOnly SeedDate = new(Year, 5, 20); // 2º trimestre
    private const int SeedQuarter = 2;

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
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "R", Debit = 1000m, Credit = 0, AccountingDate = SeedDate },
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "R", Debit = 0, Credit = 1000m, AccountingDate = SeedDate },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Expense], Ledger = "D", Debit = 300m, Credit = 0, AccountingDate = SeedDate },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Bank], Ledger = "B", Debit = 0, Credit = 300m, AccountingDate = SeedDate });
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
    public async Task Seed_quarter_includes_its_period_movements()
    {
        var tdb = await SeededAsync($"tr_{Guid.NewGuid()}");
        var s = await new StatementsService(tdb).TransparencyAsync(Year, SeedQuarter);
        Assert.Equal(700m, s.Surplus); // lançamentos caem no trimestre da competência
    }

    [Fact]
    public async Task Other_quarter_has_no_period_result()
    {
        var tdb = await SeededAsync($"tr_{Guid.NewGuid()}");
        var s = await new StatementsService(tdb).TransparencyAsync(Year, SeedQuarter + 1); // 3º trimestre
        Assert.Equal(0m, s.Surplus); // nada lançado fora do trimestre da competência
    }
}
