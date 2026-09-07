using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Demonstrações escopadas por unidade da rede (DT-14): BP/DRP/DFC por organização.</summary>
public class StatementsByUnitTests
{
    private const int Year = 2025;
    private static readonly DateOnly D = new(Year, 6, 15);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    // Duas unidades: A com receita 1000, B com receita 400 (débito Recebível / crédito Receita).
    private static async Task<(TenantDbContext tdb, Guid orgA, Guid orgB)> SeededAsync(string db)
    {
        var tdb = TDb(db);
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();

        async Task PostRevenue(Guid org, decimal amount)
        {
            var account = new Account { OrganizationId = org, Name = "Conta" };
            tdb.Accounts.Add(account);
            var t = new Transaction { AccountId = account.Id, Kind = "credit", Amount = amount, Description = "Doação", AccountingDate = D };
            tdb.Transactions.Add(t);
            tdb.AccountingEntries.AddRange(
                new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "R", Debit = amount, Credit = 0, AccountingDate = D },
                new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "R", Debit = 0, Credit = amount, AccountingDate = D });
            await tdb.SaveChangesAsync();
        }

        await PostRevenue(orgA, 1000m);
        await PostRevenue(orgB, 400m);
        return (tdb, orgA, orgB);
    }

    [Fact]
    public async Task Income_is_scoped_per_unit()
    {
        var (tdb, orgA, orgB) = await SeededAsync($"u_{Guid.NewGuid()}");
        var s = new StatementsService(tdb);

        Assert.Equal(1000m, (await s.IncomeAsync(Year, orgA)).Revenues);
        Assert.Equal(400m, (await s.IncomeAsync(Year, orgB)).Revenues);
        Assert.Equal(1400m, (await s.IncomeAsync(Year)).Revenues); // consolidado da rede
    }

    [Fact]
    public async Task Balance_sheet_is_scoped_per_unit_and_balances()
    {
        var (tdb, orgA, _) = await SeededAsync($"u_{Guid.NewGuid()}");
        var s = new StatementsService(tdb);

        var bpA = await s.BalanceSheetAsync(Year, orgA);
        Assert.Equal(1000m, bpA.Assets);   // Recebível da unidade A
        Assert.Equal(1000m, bpA.Surplus);
        Assert.True(bpA.Balanced);

        var bp = await s.BalanceSheetAsync(Year);
        Assert.Equal(1400m, bp.Assets);    // consolidado
    }

    [Fact]
    public async Task Cashflow_is_scoped_per_unit()
    {
        var tdb = TDb($"cf_{Guid.NewGuid()}");
        var treasury = new TreasuryService(tdb);
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var bankA = await treasury.CreateAccountAsync(orgA, "Banco A", "bank", 100m);
        var bankB = await treasury.CreateAccountAsync(orgB, "Banco B", "bank", 0m);

        var when = new DateTimeOffset(Year, 3, 1, 0, 0, 0, TimeSpan.Zero);
        tdb.TreasuryMovements.AddRange(
            new TreasuryMovement { AccountId = bankA.Id, Kind = "inflow", Amount = 500m, OccurredAt = when },
            new TreasuryMovement { AccountId = bankB.Id, Kind = "inflow", Amount = 90m, OccurredAt = when });
        await tdb.SaveChangesAsync();

        var s = new StatementsService(tdb);
        var a = await s.CashFlowAsync(Year, orgA);
        Assert.Equal(100m, a.OpeningCash);
        Assert.Equal(500m, a.Inflows);
        Assert.Equal(600m, a.ClosingCash);

        var all = await s.CashFlowAsync(Year);
        Assert.Equal(590m, all.Inflows); // 500 + 90 consolidado
    }
}
