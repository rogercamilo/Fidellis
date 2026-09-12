using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Audit;
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
    private sealed class NullAudit : IAuditLog
    {
        public Task RecordAsync(string action, string entity, string? entityId = null, string? metadata = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
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
        var approvals = new ApprovalService(tdb, new FixedClock(T0), new NullAudit());
        var payee = await payables.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        return (tdb, payables, approvals, payee.Id);
    }

    [Fact]
    public async Task Small_value_needs_one_coordinator_signature()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var creator = Guid.NewGuid();
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, creator);

        var approved = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "coordinator");
        Assert.Equal("approved", approved.Status);
    }

    [Fact]
    public async Task Admin_is_a_wildcard_only_in_bootstrap_and_never_self_approves()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var creator = Guid.NewGuid();
        // Faixa 500–5000 exige coordinator+council_officer; admin coringa aprova SÓ em bootstrap (D-02 Q4).
        var p = await payables.CreatePayableAsync(payee, 3000m, new DateOnly(2026, 6, 1), "Reforma", null, null, null, null, null, null, creator);

        var afterFirst = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "admin", inBootstrap: true);
        Assert.Equal("awaiting_approval", afterFirst.Status); // 2 assinaturas na faixa
        var afterSecond = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "admin", inBootstrap: true);
        Assert.Equal("approved", afterSecond.Status);

        // Segregação continua: quem lançou não aprova, nem sendo admin em bootstrap.
        var p2 = await payables.CreatePayableAsync(payee, 100m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, creator);
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p2.Id, creator, "admin", inBootstrap: true));
    }

    [Fact]
    public async Task Admin_outside_bootstrap_must_respect_the_tier()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        // 3000 → faixa coordinator+council_officer, sem admin. Fora do bootstrap, admin NÃO é coringa.
        var p = await payables.CreatePayableAsync(payee, 3000m, new DateOnly(2026, 6, 1), "Reforma", null, null, null, null, null, null, Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, Guid.NewGuid(), "admin", inBootstrap: false));
        Assert.Contains("não aprova", ex.Message);
    }

    [Fact]
    public async Task Bootstrap_override_is_recorded_in_audit()
    {
        var tdb = TDb($"apv_{Guid.NewGuid()}");
        await new FinanceConfigSeeder(tdb).EnsureDefaultsAsync();
        var payables = new PayablesService(tdb, new FixedClock(T0), new ChartOfAccountsSeeder(tdb));
        var user = new CurrentUser();
        user.SetUser(Guid.NewGuid(), "admin");
        var approvals = new ApprovalService(tdb, new FixedClock(T0), new AuditLog(tdb, user));
        var payee = await payables.CreatePayeeAsync("Fornecedor", null, null, "supplier");
        var p = await payables.CreatePayableAsync(payee.Id, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());

        await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "admin", inBootstrap: true);
        Assert.Single(tdb.AuditLog.Local, e => e.Action == "approval.bootstrap_override");
    }

    [Fact]
    public async Task Self_approval_is_blocked()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var creator = Guid.NewGuid();
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, creator);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, creator, "coordinator"));
        Assert.Contains("segregação", ex.Message);
    }

    [Fact]
    public async Task Role_outside_the_tier_cannot_approve()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());

        // Faixa até 500 aceita coordinator/council_officer — contador não aprova.
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, Guid.NewGuid(), "accountant"));
    }

    [Fact]
    public async Task High_value_requires_two_signatures()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 8000m, new DateOnly(2026, 6, 1), "Reforma", null, null, null, null, null, null, Guid.NewGuid());

        var afterFirst = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "council_officer");
        Assert.Equal("awaiting_approval", afterFirst.Status); // ainda falta 1

        var afterSecond = await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "council_chair");
        Assert.Equal("approved", afterSecond.Status);
    }

    [Fact]
    public async Task Fiscal_council_no_longer_approves_high_tier()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 8000m, new DateOnly(2026, 6, 1), "Reforma", null, null, null, null, null, null, Guid.NewGuid());

        // D-02 §4: conselho fiscal SAIU da faixa alta (fiscaliza, não autoriza).
        await Assert.ThrowsAsync<InvalidOperationException>(() => approvals.ApproveAsync(p.Id, Guid.NewGuid(), "fiscal_council"));
    }

    [Fact]
    public async Task Paying_an_approved_payable_creates_movement_and_expense()
    {
        var (tdb, payables, approvals, payee) = await SetupAsync($"apv_{Guid.NewGuid()}");
        var p = await payables.CreatePayableAsync(payee, 300m, new DateOnly(2026, 6, 1), "Material", null, null, null, null, null, null, Guid.NewGuid());
        await approvals.ApproveAsync(p.Id, Guid.NewGuid(), "coordinator");

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
