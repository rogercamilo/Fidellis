using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>MROSC + exportação para o contador (Onda 4 inc.4.3).</summary>
public class MroscExportTests
{
    private static readonly int Year = DateTimeOffset.UtcNow.Year;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Mrosc_report_sums_received_and_spent_by_project()
    {
        var tdb = TDb($"mr_{Guid.NewGuid()}");
        var project = new Project { Code = "EDITAL1", Name = "Projeto Social" };
        tdb.Projects.Add(project);
        var rec = new ReceivablesService(tdb, new SystemClock());
        await tdb.SaveChangesAsync();

        var r = await rec.CreateAsync(Guid.NewGuid(), 1000m, new DateOnly(Year, 6, 1), "grant", null, "Convênio", null, project.Id, null);
        await rec.SettleAsync(r.Id, 600m, null); // recebido 600 de 1000

        tdb.Transactions.Add(new Transaction { AccountId = Guid.NewGuid(), Kind = "debit", Amount = 400m, Description = "Gasto do projeto", ProjectId = project.Id });
        tdb.Transactions.Add(new Transaction { AccountId = Guid.NewGuid(), Kind = "debit", Amount = 999m, Description = "Outro projeto", ProjectId = Guid.NewGuid() });
        await tdb.SaveChangesAsync();

        var report = await new MroscReportService(tdb).ReportAsync(project.Id);
        Assert.NotNull(report);
        Assert.Equal(600m, report!.Received);
        Assert.Equal(400m, report.Spent);
        Assert.Equal(200m, report.Balance);
    }

    [Fact]
    public async Task Ledger_csv_has_header_and_entries()
    {
        var tdb = TDb($"ex_{Guid.NewGuid()}");
        await new ChartOfAccountsSeeder(tdb).EnsureDefaultAsync();
        var acc = await tdb.LedgerAccounts.ToDictionaryAsync(a => a.Code, a => a.Id);
        var t = new Transaction { AccountId = Guid.NewGuid(), Kind = "credit", Amount = 100m, Description = "Doação" };
        tdb.Transactions.Add(t);
        tdb.AccountingEntries.AddRange(
            new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Receivable], Ledger = "Recebível", Debit = 100m, Credit = 0 },
            new AccountingEntry { TransactionId = t.Id, LedgerAccountId = acc[ChartOfAccounts.Revenue], Ledger = "Receita", Debit = 0, Credit = 100m });
        await tdb.SaveChangesAsync();

        var csv = await new AccountantExportService(tdb, new StatementsService(tdb)).LedgerCsvAsync(Year);
        Assert.StartsWith("data;codigo;conta;debito;credito;historico", csv);
        Assert.Contains("100.00", csv);
        Assert.Contains("Doação", csv);
    }
}
