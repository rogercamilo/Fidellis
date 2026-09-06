using Fidellis.Infrastructure.Accounting;
using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Payments;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.Modules.Finance.Services;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Testes de contabilidade (plano de contas) + recibos, com EF InMemory e gateway/clock fakes.</summary>
public class AccountingTests
{
    private sealed class FakeGateway : IPaymentGateway
    {
        public Task<PixOrderResult> CreatePixOrderAsync(CreatePixOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new PixOrderResult("or_1", "ch_1", "pending", "qr", null, null));
        public Task<BoletoOrderResult> CreateBoletoOrderAsync(CreateBoletoOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new BoletoOrderResult("or_1", "ch_1", "pending", "34191", "barcode", "http://pdf", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))));
        public Task<CardChargeResult> CreateCardOrderAsync(CreateCardOrderRequest r, CancellationToken ct = default)
            => Task.FromResult(new CardChargeResult("or_1", "ch_1", "paid", null, "visa", "1234"));
        public Task<ChargeStatusResult> GetChargeAsync(string id, CancellationToken ct = default)
            => Task.FromResult(new ChargeStatusResult(id, "paid", DateTimeOffset.UtcNow));
        public Task<CreateRecipientResult> CreateRecipientAsync(CreateRecipientRequest r, CancellationToken ct = default)
            => Task.FromResult(new CreateRecipientResult("rp_1", "active"));
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private static readonly DateTimeOffset T0 = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    private static WebhookProcessor Processor(TenantDbContext tdb, IClock clock) =>
        new(tdb, new FakeGateway(), new ChartOfAccountsSeeder(tdb), new ReceiptService(tdb, clock),
            new OutboxNotifier(tdb, new MessageOutbox(tdb)), clock, NullLogger<WebhookProcessor>.Instance);

    [Fact]
    public async Task Seeder_is_idempotent()
    {
        var tdb = TDb($"acc_{Guid.NewGuid()}");
        var seeder = new ChartOfAccountsSeeder(tdb);
        await seeder.EnsureDefaultAsync();
        var first = await tdb.LedgerAccounts.CountAsync();
        await seeder.EnsureDefaultAsync();
        var second = await tdb.LedgerAccounts.CountAsync();

        Assert.Equal(first, second);
        Assert.True(first >= 4);
        Assert.Contains(await tdb.LedgerAccounts.ToListAsync(), a => a.Code == ChartOfAccounts.Receivable);
        Assert.Contains(await tdb.LedgerAccounts.ToListAsync(), a => a.Code == ChartOfAccounts.Revenue);
    }

    [Fact]
    public async Task Paid_donation_posts_to_chart_and_issues_receipt()
    {
        var tdb = TDb($"acc_{Guid.NewGuid()}");
        var org = Guid.NewGuid();
        tdb.Donations.Add(new Donation
        {
            OrganizationId = org, Amount = 100m, Method = "pix", Status = "pending",
            PspChargeId = "ch_1", DonorName = "Ana",
        });
        await tdb.SaveChangesAsync();

        await Processor(tdb, new FixedClock(T0)).ProcessAsync(
            new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid"), "{}");

        // Lançamentos nas contas do plano (RECEIVABLE/REVENUE), balanceados.
        var receivable = await tdb.LedgerAccounts.FirstAsync(a => a.Code == ChartOfAccounts.Receivable);
        var revenue = await tdb.LedgerAccounts.FirstAsync(a => a.Code == ChartOfAccounts.Revenue);
        var entries = await tdb.AccountingEntries.ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.LedgerAccountId == receivable.Id && e.Debit == 100m);
        Assert.Contains(entries, e => e.LedgerAccountId == revenue.Id && e.Credit == 100m);
        Assert.Equal(entries.Sum(e => e.Debit), entries.Sum(e => e.Credit));

        // Recibo emitido, numeração do ano.
        var receipt = await tdb.Receipts.SingleAsync();
        Assert.Equal("2026/000001", receipt.Number);
        Assert.Equal(100m, receipt.Amount);
    }

    [Fact]
    public async Task Receipt_is_not_duplicated_on_webhook_replay()
    {
        var tdb = TDb($"acc_{Guid.NewGuid()}");
        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 50m, Method = "pix", Status = "pending",
            PspChargeId = "ch_1", DonorName = "Ana",
        });
        await tdb.SaveChangesAsync();

        var processor = Processor(tdb, new FixedClock(T0));
        var evt = new PagarmeWebhookEvent("hook_1", "charge.paid", "or_1", "ch_1", "paid");
        await processor.ProcessAsync(evt, "{}");
        await processor.ProcessAsync(evt, "{}"); // reentrega

        Assert.Equal(1, await tdb.Receipts.CountAsync());
        Assert.Equal(2, await tdb.AccountingEntries.CountAsync());
    }

    [Fact]
    public async Task Receipt_numbering_is_sequential_per_org_and_year()
    {
        var tdb = TDb($"acc_{Guid.NewGuid()}");
        var org = Guid.NewGuid();
        var receipts = new ReceiptService(tdb, new FixedClock(T0));

        var d1 = new Donation { OrganizationId = org, Amount = 10m, Method = "pix", Status = "paid" };
        var d2 = new Donation { OrganizationId = org, Amount = 20m, Method = "pix", Status = "paid" };
        tdb.Donations.AddRange(d1, d2);
        await tdb.SaveChangesAsync();

        var r1 = await receipts.GenerateForDonationAsync(d1, "Ana", null);
        var r2 = await receipts.GenerateForDonationAsync(d2, "Bia", null);

        Assert.Equal("2026/000001", r1.Number);
        Assert.Equal("2026/000002", r2.Number);
    }
}
