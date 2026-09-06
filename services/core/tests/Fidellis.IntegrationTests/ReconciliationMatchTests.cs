using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Casamento de conciliação (Onda 3 inc.3.1): sugestão + baixa de AR/AP.</summary>
public class ReconciliationMatchTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static (ReconciliationMatchService match, ReceivablesService rec, PayablesService pay, TreasuryService tre) Services(TenantDbContext tdb)
    {
        var clock = new FixedClock(T0);
        var rec = new ReceivablesService(tdb, clock);
        var pay = new PayablesService(tdb, clock, new ChartOfAccountsSeeder(tdb));
        return (new ReconciliationMatchService(tdb, rec, pay), rec, pay, new TreasuryService(tdb));
    }

    private static async Task<BankStatementLine> LineAsync(TenantDbContext tdb, Guid accountId, decimal amount, DateOnly posted, string? memo = null)
    {
        var stmt = new BankStatement { AccountId = accountId, Format = "ofx" };
        tdb.BankStatements.Add(stmt);
        var line = new BankStatementLine { StatementId = stmt.Id, Amount = amount, PostedAt = posted, Memo = memo };
        tdb.BankStatementLines.Add(line);
        await tdb.SaveChangesAsync();
        return line;
    }

    [Fact]
    public async Task Suggests_receivable_by_amount_and_date_window()
    {
        var tdb = TDb($"rm_{Guid.NewGuid()}");
        var (match, rec, _, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        await rec.CreateAsync(Guid.NewGuid(), 300m, new DateOnly(2026, 5, 18), "pledge", null, "Dízimo", null, null, null);
        var line = await LineAsync(tdb, acc.Id, 300m, new DateOnly(2026, 5, 20)); // 2 dias de diferença

        var candidates = await match.SuggestAsync(line.Id);
        Assert.Single(candidates);
        Assert.Equal("receivable", candidates[0].Type);
        Assert.Equal(300m, candidates[0].Amount);
    }

    [Fact]
    public async Task Matching_receivable_settles_and_records_inflow()
    {
        var tdb = TDb($"rm_{Guid.NewGuid()}");
        var (match, rec, _, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var r = await rec.CreateAsync(Guid.NewGuid(), 300m, new DateOnly(2026, 5, 18), "pledge", null, null, null, null, null);
        var line = await LineAsync(tdb, acc.Id, 300m, new DateOnly(2026, 5, 20));

        await match.MatchAsync(line.Id, "receivable", r.Id);

        Assert.Equal("received", (await tdb.Receivables.SingleAsync()).Status);
        Assert.Equal("matched", (await tdb.BankStatementLines.SingleAsync()).Status);
        Assert.Equal(300m, await tre.AccountBalanceAsync(acc.Id)); // entrada registrada
    }

    [Fact]
    public async Task Matching_payable_pays_it()
    {
        var tdb = TDb($"rm_{Guid.NewGuid()}");
        var (match, _, pay, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 1000m);
        var payee = await pay.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        var p = await pay.CreatePayableAsync(payee.Id, 120m, new DateOnly(2026, 5, 19), "Material", null, null, null, null, null, null, null);
        p.Status = "approved"; p.ApprovedAt = T0; await tdb.SaveChangesAsync();
        var line = await LineAsync(tdb, acc.Id, -120m, new DateOnly(2026, 5, 20));

        await match.MatchAsync(line.Id, "payable", p.Id);

        Assert.Equal("paid", (await tdb.Payables.SingleAsync()).Status);
        Assert.Equal("matched", (await tdb.BankStatementLines.SingleAsync()).Status);
        Assert.Equal(880m, await tre.AccountBalanceAsync(acc.Id)); // 1000 - 120
    }

    [Fact]
    public async Task Ignore_marks_line_ignored()
    {
        var tdb = TDb($"rm_{Guid.NewGuid()}");
        var (match, _, _, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var line = await LineAsync(tdb, acc.Id, 50m, new DateOnly(2026, 5, 20));

        await match.IgnoreAsync(line.Id);
        Assert.Equal("ignored", (await tdb.BankStatementLines.SingleAsync()).Status);
    }
}
