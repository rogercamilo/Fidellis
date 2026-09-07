using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Demonstrações ITG 2002 (Onda 4 inc.4.0): balancete, DRP e Balanço Patrimonial.</summary>
public class StatementsReportTests
{
    // Competência fixa (DT-07/DT-13): período histórico determinístico, não "ano corrente".
    private const int Year = 2025;
    private static readonly DateOnly D = new(Year, 6, 15);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    /// <summary>Cenário balanceado: receita 1000 (débito Recebível / crédito Receita) e despesa 300 (débito Despesa / crédito Banco).</summary>
    private static async Task<TenantDbContext> SeededAsync(string db)
    {
        var tdb = TDb(db);
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);

        var tRev = new Transaction { AccountId = Guid.NewGuid(), Kind = "credit", Amount = 1000m, Description = "Doação" };
        var tExp = new Transaction { AccountId = Guid.NewGuid(), Kind = "debit", Amount = 300m, Description = "Despesa" };
        tdb.Transactions.AddRange(tRev, tExp);
        tdb.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "Recebível", Debit = 1000m, Credit = 0, AccountingDate = D },
            new AccountingEntry { TransactionId = tRev.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "Receita", Debit = 0, Credit = 1000m, AccountingDate = D },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Expense], Ledger = "Despesa", Debit = 300m, Credit = 0, AccountingDate = D },
            new AccountingEntry { TransactionId = tExp.Id, LedgerAccountId = acc[ChartOfAccounts.Bank], Ledger = "Banco", Debit = 0, Credit = 300m, AccountingDate = D });
        await tdb.SaveChangesAsync();
        return tdb;
    }

    [Fact]
    public async Task Trial_balance_is_balanced()
    {
        var tdb = await SeededAsync($"st_{Guid.NewGuid()}");
        var lines = await new StatementsService(tdb).TrialBalanceAsync(Year);
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit)); // 1300 = 1300
    }

    [Fact]
    public async Task Income_computes_surplus()
    {
        var tdb = await SeededAsync($"st_{Guid.NewGuid()}");
        var drp = await new StatementsService(tdb).IncomeAsync(Year);
        Assert.Equal(1000m, drp.Revenues);
        Assert.Equal(300m, drp.Expenses);
        Assert.Equal(700m, drp.Surplus);
    }

    [Fact]
    public async Task Balance_sheet_balances_with_surplus_in_equity()
    {
        var tdb = await SeededAsync($"st_{Guid.NewGuid()}");
        var bp = await new StatementsService(tdb).BalanceSheetAsync(Year);
        // Ativo = Recebível 1000 + Banco -300 = 700; Passivo 0; PL 0 + superávit 700.
        Assert.Equal(700m, bp.Assets);
        Assert.Equal(700m, bp.Surplus);
        Assert.Equal(700m, bp.TotalLiabilitiesAndEquity);
        Assert.True(bp.Balanced);
    }
}
