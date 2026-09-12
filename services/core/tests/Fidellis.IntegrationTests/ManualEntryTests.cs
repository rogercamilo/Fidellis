using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Lançamento manual de entrada (D-05): recebimento fora do PSP vira entrada de 1ª classe.</summary>
public class ManualEntryTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static (ManualEntryService entries, TreasuryService treasury) Services(TenantDbContext tdb)
    {
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var recon = new ReconciliationService(tdb, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock);
        return (new ManualEntryService(tdb, recon, clock), treasury);
    }

    [Fact]
    public async Task Manual_entry_to_bank_with_donor_posts_revenue_and_receipt()
    {
        var tdb = TDb($"me_{Guid.NewGuid()}");
        var (entries, treasury) = Services(tdb);
        var org = Guid.NewGuid();
        var banco = await treasury.CreateAccountAsync(org, "Banco", "bank", 0m);

        var entry = await entries.CreateAsync(new ManualEntryCommand(
            banco.Id, 250m, "Maria", "maria@ex.com", "12345678900", null, null, null, null));

        Assert.Equal("manual", entry.Source);
        Assert.Equal("paid", entry.Status);
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());          // débito Banco / crédito Receita
        Assert.Equal(250m, await treasury.AccountBalanceAsync(banco.Id));   // entrou no banco
        var bankLedger = await tdb.LedgerAccounts.FirstAsync(a => a.Code == ChartOfAccounts.Bank);
        Assert.Equal(250m, await tdb.AccountingEntries.Where(e => e.LedgerAccountId == bankLedger.Id).SumAsync(e => e.Debit));
        Assert.Single(await tdb.Receipts.ToListAsync());                   // doador identificado → recibo (Q3)
    }

    [Fact]
    public async Task Manual_entry_without_donor_has_no_receipt()
    {
        var tdb = TDb($"me_{Guid.NewGuid()}");
        var (entries, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);

        var entry = await entries.CreateAsync(new ManualEntryCommand(
            caixa.Id, 90m, null, null, null, null, null, null, null));

        Assert.Equal("manual", entry.Source);
        var caixaLedger = await tdb.LedgerAccounts.FirstAsync(a => a.Code == ChartOfAccounts.Cash);
        Assert.Equal(90m, await tdb.AccountingEntries.Where(e => e.LedgerAccountId == caixaLedger.Id).SumAsync(e => e.Debit));
        Assert.Empty(await tdb.Receipts.ToListAsync());                    // sem doador → sem recibo
    }

    [Fact]
    public async Task Manual_entry_rejects_nonpositive_amount_and_unknown_account()
    {
        var tdb = TDb($"me_{Guid.NewGuid()}");
        var (entries, treasury) = Services(tdb);
        var banco = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);

        await Assert.ThrowsAsync<ArgumentException>(() => entries.CreateAsync(new ManualEntryCommand(
            banco.Id, 0m, null, null, null, null, null, null, null)));
        await Assert.ThrowsAsync<ArgumentException>(() => entries.CreateAsync(new ManualEntryCommand(
            Guid.NewGuid(), 50m, null, null, null, null, null, null, null)));
    }
}
