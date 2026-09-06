using Fidellis.Infrastructure.Messaging;
using Fidellis.Infrastructure.Persistence;
using Fidellis.Infrastructure.TenantData;
using Fidellis.Modules.Finance.Notifications;
using Fidellis.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Testes da régua de relacionamento (outbox, notifier, dispatcher, reativação, templates).</summary>
public class MessagingTests
{
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; set; } = now; }

    private sealed class FakeSender(string channel, SendResult result) : IMessageSender
    {
        public string Channel => channel;
        public Task<SendResult> SendAsync(OutboxMessage message, CancellationToken ct = default) => Task.FromResult(result);
    }

    private static readonly DateTimeOffset T0 = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static TenantDbContext TDb(string db)
    {
        var tenant = new TenantContext();
        tenant.SetTenant("diocese-sp");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseInMemoryDatabase(db).Options, tenant);
    }

    [Fact]
    public void Templates_render_thank_you_with_amount_and_receipt()
    {
        var m = MessageTemplates.Render(MessageTemplates.ThankYou,
            new MessageContext("Ana", "Paróquia X", 100m, "2026/000001"));
        Assert.False(string.IsNullOrWhiteSpace(m.Subject));
        Assert.Contains("Ana", m.Body);
        Assert.Contains("2026/000001", m.Body);
    }

    [Fact]
    public async Task Outbox_is_idempotent_by_dedupe_key()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        var outbox = new MessageOutbox(tdb);
        var req = new EnqueueRequest("thank_you", "ana@x.org", "Obrigado", "corpo", DedupeKey: "k1");
        await outbox.EnqueueAsync(req);
        await outbox.EnqueueAsync(req);
        Assert.Equal(1, await tdb.Messages.CountAsync());
    }

    [Fact]
    public async Task OutboxNotifier_enqueues_thank_you_on_payment()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        var donor = new Donor { Name = "Ana", Email = "ana@x.org" };
        tdb.Donors.Add(donor);
        var donation = new Donation { OrganizationId = Guid.NewGuid(), Amount = 80m, Method = "pix", Status = "paid", DonorId = donor.Id };
        tdb.Donations.Add(donation);
        await tdb.SaveChangesAsync();

        await new OutboxNotifier(tdb, new MessageOutbox(tdb)).DonationPaidAsync(donation, "2026/000009");

        var msg = await tdb.Messages.SingleAsync();
        Assert.Equal("thank_you", msg.EventType);
        Assert.Equal("ana@x.org", msg.ToAddress);
        Assert.Equal("queued", msg.Status);
    }

    [Fact]
    public async Task OutboxNotifier_enqueues_dunning_and_past_due()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        var donor = new Donor { Name = "Bia", Email = "bia@x.org" };
        tdb.Donors.Add(donor);
        var rec = new RecurringDonation { OrganizationId = Guid.NewGuid(), DonorId = donor.Id, Amount = 50m, Status = "active", NextChargeAt = T0 };
        tdb.RecurringDonations.Add(rec);
        await tdb.SaveChangesAsync();

        var notifier = new OutboxNotifier(tdb, new MessageOutbox(tdb));
        await notifier.PaymentFailedAsync(rec, 1);
        await notifier.PastDueAsync(rec);

        var types = await tdb.Messages.Select(m => m.EventType).ToListAsync();
        Assert.Contains("payment_failed", types);
        Assert.Contains("past_due", types);
    }

    [Fact]
    public async Task Dispatcher_sends_queued_and_marks_sent()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        await new MessageOutbox(tdb).EnqueueAsync(new EnqueueRequest("thank_you", "ana@x.org", "s", "b"));

        var dispatcher = new MessageDispatcher(tdb, [new FakeSender("email", SendResult.Sent)], new FixedClock(T0), NullLogger<MessageDispatcher>.Instance);
        var sent = await dispatcher.DispatchQueuedAsync();

        Assert.Equal(1, sent);
        var msg = await tdb.Messages.SingleAsync();
        Assert.Equal("sent", msg.Status);
        Assert.NotNull(msg.SentAt);
    }

    [Fact]
    public async Task Dispatcher_marks_failed_on_sender_error()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        await new MessageOutbox(tdb).EnqueueAsync(new EnqueueRequest("thank_you", "ana@x.org", "s", "b"));

        var dispatcher = new MessageDispatcher(tdb, [new FakeSender("email", SendResult.Failed("boom"))], new FixedClock(T0), NullLogger<MessageDispatcher>.Instance);
        await dispatcher.DispatchQueuedAsync();

        Assert.Equal("failed", (await tdb.Messages.SingleAsync()).Status);
    }

    [Fact]
    public async Task Reactivation_enqueues_inactive_donor_once()
    {
        var tdb = TDb($"msg_{Guid.NewGuid()}");
        var donor = new Donor { Name = "Léo", Email = "leo@x.org" };
        tdb.Donors.Add(donor);
        tdb.Donations.Add(new Donation
        {
            OrganizationId = Guid.NewGuid(), Amount = 30m, Method = "pix", Status = "paid",
            DonorId = donor.Id, PaidAt = T0.AddDays(-200),
        });
        await tdb.SaveChangesAsync();

        var scanner = new ReactivationScanner(tdb, new MessageOutbox(tdb), new FixedClock(T0));
        var first = await scanner.EnqueueInactiveAsync(90);
        var second = await scanner.EnqueueInactiveAsync(90);

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(1, await tdb.Messages.CountAsync(m => m.EventType == "reactivation"));
    }

    [Fact]
    public void Resend_payload_contains_fields()
    {
        var json = ResendEmailSender.BuildPayload("Fidellis <no@x.org>", "ana@x.org", "Assunto", "Corpo");
        Assert.Contains("ana@x.org", json);
        Assert.Contains("Assunto", json);
        Assert.Contains("Fidellis", json);
    }
}
