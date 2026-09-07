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

/// <summary>Bookkeeping consistente (DT-02): a doação recebida fecha o Balanço e reflete na tesouraria.</summary>
public class BookkeepingConsistencyTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(DateTimeOffset.UtcNow.Year, 5, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly int Year = T0.Year;

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static ReconciliationService Recon(TenantDbContext tdb, IClock clock) =>
        new(tdb, new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock);

    [Fact]
    public async Task Balance_sheet_balances_after_a_real_donation()
    {
        var tdb = TDb($"bk_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var org = Guid.NewGuid();

        var donation = new Donation { OrganizationId = org, Amount = 100m, Method = "pix", Status = "paid", PaidAt = T0, DonorName = "Ana" };
        tdb.Donations.Add(donation);
        await tdb.SaveChangesAsync();

        await Recon(tdb, clock).PostPaidAsync(donation);
        await tdb.SaveChangesAsync();

        var bp = await new StatementsService(tdb).BalanceSheetAsync(Year);
        Assert.True(bp.Balanced);          // Ativo = Passivo + PL (com o superávit)
        Assert.Equal(100m, bp.Assets);     // Banco (não mais "a receber")
        Assert.Equal(100m, bp.Surplus);
    }

    [Fact]
    public async Task Donation_reflects_in_treasury_bank_account()
    {
        var tdb = TDb($"bk_{Guid.NewGuid()}");
        var clock = new FixedClock(T0);
        var treasury = new TreasuryService(tdb);
        var org = Guid.NewGuid();
        var bank = await treasury.CreateAccountAsync(org, "Banco", "bank", 0m);

        var donation = new Donation { OrganizationId = org, Amount = 250m, Method = "pix", Status = "paid", PaidAt = T0, DonorName = "Ana" };
        tdb.Donations.Add(donation);
        await tdb.SaveChangesAsync();

        await Recon(tdb, clock).PostPaidAsync(donation);
        await tdb.SaveChangesAsync();

        Assert.Equal(250m, await treasury.AccountBalanceAsync(bank.Id)); // entrada de tesouraria (DT-02)
    }
}
