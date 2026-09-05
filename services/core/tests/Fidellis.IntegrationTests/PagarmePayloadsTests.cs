using System.Text.Json;
using Fidellis.Infrastructure.Payments;
using Xunit;

namespace Fidellis.IntegrationTests;

public class PagarmePayloadsTests
{
    [Theory]
    [InlineData(100.00, 10000)]
    [InlineData(49.90, 4990)]
    [InlineData(0.01, 1)]
    public void ToCents_converts_reais_to_cents(decimal amount, int expected)
        => Assert.Equal(expected, PagarmePayloads.ToCents(amount));

    [Fact]
    public void BuildPixOrder_sets_amount_customer_and_pix_method()
    {
        var json = PagarmePayloads.BuildPixOrder(new CreatePixOrderRequest(
            Amount: 100m, DonorName: "Ana", DonorEmail: "ana@x.org", DonorDocument: "12345678900"));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(10000, root.GetProperty("items")[0].GetProperty("amount").GetInt32());
        Assert.Equal("Ana", root.GetProperty("customer").GetProperty("name").GetString());
        Assert.Equal("pix", root.GetProperty("payments")[0].GetProperty("payment_method").GetString());
    }

    [Fact]
    public void BuildPixOrder_adds_split_when_recipient_present()
    {
        var json = PagarmePayloads.BuildPixOrder(new CreatePixOrderRequest(
            Amount: 50m, DonorName: "Ana", DonorEmail: "ana@x.org", DonorDocument: "12345678900",
            RecipientId: "rp_123"));

        using var doc = JsonDocument.Parse(json);
        var split = doc.RootElement.GetProperty("payments")[0].GetProperty("split")[0];
        Assert.Equal("rp_123", split.GetProperty("recipient_id").GetString());
        Assert.Equal(100, split.GetProperty("amount").GetInt32());
    }

    [Fact]
    public void ParsePixOrderResponse_extracts_ids_and_qr()
    {
        const string json = """
            {
              "id": "or_abc",
              "charges": [{
                "id": "ch_xyz",
                "status": "pending",
                "last_transaction": {
                  "qr_code": "00020126PIX",
                  "qr_code_url": "https://pagar.me/qr/ch_xyz",
                  "expires_at": "2026-09-05T12:00:00Z"
                }
              }]
            }
            """;

        var result = PagarmePayloads.ParsePixOrderResponse(json);
        Assert.Equal("or_abc", result.OrderId);
        Assert.Equal("ch_xyz", result.ChargeId);
        Assert.Equal("pending", result.Status);
        Assert.Equal("00020126PIX", result.QrCode);
        Assert.Equal("https://pagar.me/qr/ch_xyz", result.QrCodeUrl);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public void ParseChargeResponse_reads_status_and_paid_at()
    {
        const string json = """{ "id": "ch_1", "status": "paid", "paid_at": "2026-09-05T12:30:00Z" }""";
        var result = PagarmePayloads.ParseChargeResponse(json);
        Assert.Equal("ch_1", result.ChargeId);
        Assert.Equal("paid", result.Status);
        Assert.NotNull(result.PaidAt);
    }
}
