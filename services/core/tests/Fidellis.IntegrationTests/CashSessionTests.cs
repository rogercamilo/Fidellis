using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Caixa físico (Onda 2 inc.2.5): abertura, fechamento com dupla conferência e depósito.</summary>
public class CashSessionTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 10, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static (CashSessionService sessions, TreasuryService treasury) Services(TenantDbContext tdb)
    {
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var recon = new ReconciliationService(tdb, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock);
        return (new CashSessionService(tdb, treasury, recon, clock), treasury);
    }

    [Fact]
    public async Task Close_requires_a_second_person()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa Missa", "cash", 0m);
        var opener = Guid.NewGuid();
        var s = await sessions.OpenAsync(caixa.Id, opener, "Missa dom 10h");

        // Mesmo responsável não pode conferir.
        await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.CloseAsync(s.Id, 500m, opener));
    }

    [Fact]
    public async Task Close_with_second_person_adds_collection_to_cash_balance()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);
        var s = await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), "Culto");

        var closed = await sessions.CloseAsync(s.Id, 500m, Guid.NewGuid());
        Assert.Equal("closed", closed.Status);
        Assert.Equal(500m, await treasury.AccountBalanceAsync(caixa.Id)); // coleta entrou no caixa
    }

    [Fact]
    public async Task Close_generates_a_first_class_cash_entry()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);
        var s = await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), "Culto");
        await sessions.CloseAsync(s.Id, 500m, Guid.NewGuid());

        // D-05: a coleta vira entrada de 1ª classe — Entry (source=cash, paid) + partida dobrada.
        var entry = await tdb.Entries.SingleAsync();
        Assert.Equal("cash", entry.Source);
        Assert.Equal(EntryTypes.Offering, entry.EntryType);           // agregada default = oferta (D-06)
        Assert.Equal("paid", entry.Status);
        Assert.Equal(500m, entry.Amount);
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());     // débito Caixa / crédito Receita
        var caixaLedger = await tdb.LedgerAccounts.FirstAsync(a => a.Code == ChartOfAccounts.Cash);
        Assert.Equal(500m, await tdb.AccountingEntries.Where(e => e.LedgerAccountId == caixaLedger.Id).SumAsync(e => e.Debit));
        Assert.Empty(await tdb.Receipts.ToListAsync());               // coleta anônima: sem recibo (Q3)
    }

    [Fact]
    public async Task Close_discriminated_creates_one_entry_per_type()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);
        var s = await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), "Culto");

        await sessions.CloseAsync(s.Id, 300m, Guid.NewGuid(), new[]
        {
            new CashEntryLine(EntryTypes.Tithe, 200m),
            new CashEntryLine(EntryTypes.Offering, 100m),
        });

        var entries = await tdb.Entries.ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.EntryType == EntryTypes.Tithe && e.Amount == 200m);
        Assert.Contains(entries, e => e.EntryType == EntryTypes.Offering && e.Amount == 100m);
        Assert.Equal(300m, await treasury.AccountBalanceAsync(caixa.Id)); // Σ = conferido
    }

    [Fact]
    public async Task Close_discriminated_rejects_sum_mismatch()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);
        var s = await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.CloseAsync(
            s.Id, 300m, Guid.NewGuid(), new[] { new CashEntryLine(EntryTypes.Tithe, 200m) }));
    }

    [Fact]
    public async Task Deposit_transfers_from_cash_to_bank()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var org = Guid.NewGuid();
        var caixa = await treasury.CreateAccountAsync(org, "Caixa", "cash", 0m);
        var banco = await treasury.CreateAccountAsync(org, "Banco", "bank", 0m);

        var s = await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), "Missa");
        await sessions.CloseAsync(s.Id, 500m, Guid.NewGuid());
        var deposited = await sessions.DepositAsync(s.Id, banco.Id);

        Assert.NotNull(deposited.DepositedMovementId);
        Assert.Equal(0m, await treasury.AccountBalanceAsync(caixa.Id));   // 500 - 500
        Assert.Equal(500m, await treasury.AccountBalanceAsync(banco.Id)); // depósito
    }

    [Fact]
    public async Task Cannot_open_on_a_bank_account_or_twice()
    {
        var tdb = TDb($"cs_{Guid.NewGuid()}");
        var (sessions, treasury) = Services(tdb);
        var banco = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 0m);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.OpenAsync(banco.Id, Guid.NewGuid(), null));

        var caixa = await treasury.CreateAccountAsync(Guid.NewGuid(), "Caixa", "cash", 0m);
        await sessions.OpenAsync(caixa.Id, Guid.NewGuid(), null);
        await Assert.ThrowsAsync<InvalidOperationException>(() => sessions.OpenAsync(caixa.Id, Guid.NewGuid(), null)); // já aberta
    }
}
