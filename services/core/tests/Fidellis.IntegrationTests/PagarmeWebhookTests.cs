using Fidellis.Infrastructure.Payments;
using Xunit;

namespace Fidellis.IntegrationTests;

public class PagarmeWebhookTests
{
    [Fact]
    public void Parse_charge_event_reads_charge_order_and_status()
    {
        const string json = """
            {
              "id": "hook_1",
              "type": "charge.paid",
              "data": { "id": "ch_1", "status": "paid", "order": { "id": "or_1" } }
            }
            """;

        var evt = PagarmeWebhook.Parse(json);
        Assert.Equal("hook_1", evt.EventId);
        Assert.Equal("charge.paid", evt.Type);
        Assert.Equal("ch_1", evt.ChargeId);
        Assert.Equal("or_1", evt.OrderId);
        Assert.Equal("paid", evt.ChargeStatus);
    }

    [Fact]
    public void Parse_order_event_reads_order_and_first_charge()
    {
        const string json = """
            {
              "id": "hook_2",
              "type": "order.paid",
              "data": { "id": "or_2", "status": "paid", "charges": [{ "id": "ch_2", "status": "paid" }] }
            }
            """;

        var evt = PagarmeWebhook.Parse(json);
        Assert.Equal("or_2", evt.OrderId);
        Assert.Equal("ch_2", evt.ChargeId);
        Assert.Equal("paid", evt.ChargeStatus);
    }
}
