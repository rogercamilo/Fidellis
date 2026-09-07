using Fidellis.Infrastructure.Accounting;
using Xunit;

namespace Fidellis.IntegrationTests;

/// <summary>Geração de PDF do recibo (QuestPDF): produz um arquivo PDF válido, sem depender de storage.</summary>
public class ReceiptPdfTests
{
    static ReceiptPdfTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    [Fact]
    public void Renders_a_valid_pdf()
    {
        var pdf = new ReceiptPdfService();
        var bytes = pdf.Render(new ReceiptData(
            Number: "2025/000123",
            OrganizationName: "Paróquia São Pedro",
            DonorName: "Ana Silva",
            DonorDocument: "123.456.789-00",
            Amount: 250.00m,
            IssuedAt: new DateTimeOffset(2025, 5, 20, 12, 0, 0, TimeSpan.Zero)));

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000);
        // Assinatura de arquivo PDF: "%PDF".
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
    }
}
