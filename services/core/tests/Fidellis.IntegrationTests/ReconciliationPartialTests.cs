using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Conciliação: baixa parcial, N:1 (uma linha → vários títulos) e 1:N (várias linhas → um título) — DT-08.</summary>
public class ReconciliationPartialTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Due = new(2026, 5, 19);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static (ReconciliationMatchService match, ReceivablesService rec, TreasuryService tre) Services(TenantDbContext tdb)
    {
        var clock = new FixedClock(T0);
        var rec = new ReceivablesService(tdb, clock);
        var pay = new PayablesService(tdb, clock, new ChartOfAccountsSeeder(tdb));
        return (new ReconciliationMatchService(tdb, rec, pay), rec, new TreasuryService(tdb));
    }

    private static async Task<BankStatementLine> LineAsync(TenantDbContext tdb, Guid accountId, decimal amount)
    {
        var stmt = new BankStatement { AccountId = accountId, Format = "ofx" };
        tdb.BankStatements.Add(stmt);
        var line = new BankStatementLine { StatementId = stmt.Id, Amount = amount, PostedAt = new DateOnly(2026, 5, 20) };
        tdb.BankStatementLines.Add(line);
        await tdb.SaveChangesAsync();
        return line;
    }

    [Fact]
    public async Task Partial_match_leaves_receivable_partial_and_line_matched()
    {
        var tdb = TDb($"rp_{Guid.NewGuid()}");
        var (match, rec, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var r = await rec.CreateAsync(Guid.NewGuid(), 300m, Due, "pledge", null, null, null, null, null);
        var line = await LineAsync(tdb, acc.Id, 100m); // linha menor que o título

        await match.MatchAsync(line.Id, "receivable", r.Id);

        Assert.Equal("partial", (await tdb.Receivables.SingleAsync()).Status);
        Assert.Equal("matched", (await tdb.BankStatementLines.SingleAsync()).Status);
        Assert.Equal(100m, await tre.AccountBalanceAsync(acc.Id));
    }

    [Fact]
    public async Task One_line_settles_many_receivables_N_to_1()
    {
        var tdb = TDb($"rp_{Guid.NewGuid()}");
        var (match, rec, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var a = await rec.CreateAsync(Guid.NewGuid(), 300m, Due, "pledge", null, null, null, null, null);
        var b = await rec.CreateAsync(Guid.NewGuid(), 200m, Due, "pledge", null, null, null, null, null);
        var line = await LineAsync(tdb, acc.Id, 500m); // um depósito cobre os dois títulos

        var afterFirst = await match.MatchAsync(line.Id, "receivable", a.Id);
        Assert.Equal("partial", afterFirst.Status); // ainda sobra saldo na linha

        var afterSecond = await match.MatchAsync(line.Id, "receivable", b.Id);
        Assert.Equal("matched", afterSecond.Status);

        Assert.All(await tdb.Receivables.ToListAsync(), r => Assert.Equal("received", r.Status));
        Assert.Equal(500m, await tre.AccountBalanceAsync(acc.Id));
    }

    [Fact]
    public async Task Many_lines_settle_one_receivable_1_to_N()
    {
        var tdb = TDb($"rp_{Guid.NewGuid()}");
        var (match, rec, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        var r = await rec.CreateAsync(Guid.NewGuid(), 300m, Due, "pledge", null, null, null, null, null);
        var line1 = await LineAsync(tdb, acc.Id, 100m);
        var line2 = await LineAsync(tdb, acc.Id, 200m);

        await match.MatchAsync(line1.Id, "receivable", r.Id);
        Assert.Equal("partial", (await tdb.Receivables.SingleAsync()).Status);

        await match.MatchAsync(line2.Id, "receivable", r.Id);
        Assert.Equal("received", (await tdb.Receivables.SingleAsync()).Status);
        Assert.Equal(300m, await tre.AccountBalanceAsync(acc.Id));
    }

    [Fact]
    public async Task Suggests_partial_candidate_when_line_is_smaller()
    {
        var tdb = TDb($"rp_{Guid.NewGuid()}");
        var (match, rec, tre) = Services(tdb);
        var acc = await tre.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        await rec.CreateAsync(Guid.NewGuid(), 300m, Due, "pledge", null, "Dízimo", null, null, null);
        var line = await LineAsync(tdb, acc.Id, 100m);

        var candidates = await match.SuggestAsync(line.Id);
        var c = Assert.Single(candidates);
        Assert.Equal("partial", c.Kind);
        Assert.Equal(300m, c.Amount); // saldo do título
    }
}
