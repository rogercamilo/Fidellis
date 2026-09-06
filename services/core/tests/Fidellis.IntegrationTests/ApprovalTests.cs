using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Configuration;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Alçadas de aprovação + pagamento (Onda 2 inc.2.3): guarda-corpos de compliance.</summary>
public class ApprovalTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }
    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static async Task<(TenantDbContext, PayablesService, ApprovalService, Guid payeeId)> SetupAsync(string db)
    {
        var tdb = TDb(db);
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync();   // semeia as 3 faixas default
        var payables = new PayablesService(tdb, new FixedClock(T0), new ChartOfAccountsSeeder(tdb));
        var approvals = new ApprovalService(tdb, new FixedClock(T0));
        var payee = await payables.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        return (tdb, payables, approvals, payee.Id);
    }

    [Fact]
    public async Task Small_value_needs_one_treasurer_signature()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var creator = Guid.NewGuid();
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, creator);

        var approved = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "treasurer");
        Assert.Equal("approved", approved.Status);
    }

    [Fact]
    public async Task Self_approval_is_blocked()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var creator = Guid.NewGuid();
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, creator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, creator, "treasurer"));
        Assert.Contains("segregação", ex.Message);
    }

    [Fact]
    public async Task Role_outside_the_tier_cannot_approve()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());

        // Faixa até 500 só aceita 'treasurer'.
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, Guid.NewGuid(), "accountant"));
    }

    [Fact]
    public async Task High_value_requires_two_signatures()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 8000m, new DateOnly(2026, 6, 1), "Reforma", null, null, null, null, null, null, Guid.NewGuid());

        var afterFirst = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "manager");
        Assert.Equal("awaiting_approval", afterFirst.Status); // ainda falta 1

        var afterSecond = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "fiscal_council");
        Assert.Equal("approved", afterSecond.Status);
    }

    [Fact]
    public async Task Paying_an_approved_payable_creates_movement_and_expense()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());
        await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "treasurer");

        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 1000m);

        var paid = await payables.PayAsync(p.Id, acc.Id);
        Assert.Equal("paid", paid!.Status);
        Assert.Equal(700m, await treasury.AccountBalanceAsync(acc.Id));       // 1000 - 300
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());            // débito Despesa / crédito Banco
    }

    [Fact]
    public async Task Cannot_pay_a_payable_that_is_not_approved()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());
        var treasury = new TreasuryService(tdb);
        var acc = await treasury.CreateAccountAsync(Guid.NewGuid(), "Banco", "bank", 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => payables.PayAsync(p.Id, acc.Id));
    }
}
