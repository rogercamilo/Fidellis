using Fidellis.Infrastructure.Persistence;
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
        var treasury = new TreasuryService(tdb);
        return (new CashSessionService(tdb, treasury, new FixedClock(T0)), treasury);
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
