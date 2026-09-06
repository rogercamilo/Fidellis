using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Tesouraria (Onda 2 inc.2.0): saldo, transferências e consolidado por unidade.</summary>
public class TreasuryTests
{
    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public async Task Balance_is_opening_plus_movements()
    {
        var tdb = TDb($"tre_{Guid.NewGuid()}");
        var svc = new TreasuryService(tdb);
        var org = Guid.NewGuid();
        var acc = await svc.CreateAccountAsync(org, "Conta Itaú", "bank", 100m);

        tdb.TreasuryMovements.AddRange(
            new TreasuryMovement { AccountId = acc.Id, Kind = "inflow", Amount = 50m },
            new TreasuryMovement { AccountId = acc.Id, Kind = "outflow", Amount = 30m });
        await tdb.SaveChangesAsync();

        Assert.Equal(120m, await svc.AccountBalanceAsync(acc.Id)); // 100 + 50 - 30
    }

    [Fact]
    public async Task Transfer_moves_balance_without_changing_the_total()
    {
        var tdb = TDb($"tre_{Guid.NewGuid()}");
        var svc = new TreasuryService(tdb);
        var org = Guid.NewGuid();
        var caixa = await svc.CreateAccountAsync(org, "Caixa", "cash", 200m);
        var banco = await svc.CreateAccountAsync(org, "Banco", "bank", 0m);

        await svc.TransferAsync(caixa.Id, banco.Id, 80m, "Depósito");

        Assert.Equal(120m, await svc.AccountBalanceAsync(caixa.Id));
        Assert.Equal(80m, await svc.AccountBalanceAsync(banco.Id));
        // Consolidado das duas contas não muda com a transferência interna.
        Assert.Equal(200m, await svc.ConsolidatedBalanceAsync([org]));
    }

    [Fact]
    public async Task Transfer_rejects_same_account_and_nonpositive_amount()
    {
        var tdb = TDb($"tre_{Guid.NewGuid()}");
        var svc = new TreasuryService(tdb);
        var acc = await svc.CreateAccountAsync(Guid.NewGuid(), "X", "bank", 0m);

        await Assert.ThrowsAsync<ArgumentException>(() => svc.TransferAsync(acc.Id, acc.Id, 10m, null));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.TransferAsync(acc.Id, Guid.NewGuid(), 0m, null));
    }

    [Fact]
    public async Task Consolidated_sums_only_the_given_organizations()
    {
        var tdb = TDb($"tre_{Guid.NewGuid()}");
        var svc = new TreasuryService(tdb);
        var paroquiaA = Guid.NewGuid();
        var paroquiaB = Guid.NewGuid();
        await svc.CreateAccountAsync(paroquiaA, "A", "bank", 100m);
        await svc.CreateAccountAsync(paroquiaB, "B", "bank", 250m);

        Assert.Equal(350m, await svc.ConsolidatedBalanceAsync([paroquiaA, paroquiaB]));
        Assert.Equal(100m, await svc.ConsolidatedBalanceAsync([paroquiaA]));
    }
}
