using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Fidellis.Infrastructure.Accounting;

/// <summary>Dados do recibo para renderização do PDF (espelha o layout do front recibo/[id]).</summary>
public sealed record ReceiptData(
    string Number, string? OrganizationName, string DonorName, string? DonorDocument,
    decimal Amount, DateTimeOffset IssuedAt);

/// <summary>
/// Renderiza o recibo de doação em PDF (QuestPDF). Layout espelha o HTML de <c>recibo/[id]/page.tsx</c>:
/// cabeçalho Fidellis + nº, dados de instituição/doador, valor em destaque e texto de prestação de contas.
/// </summary>
public sealed class ReceiptPdfService
{
    private static readonly string[] Currency = ["R$"];

    public byte[] Render(ReceiptData r)
    {
        var brl = r.Amount.ToString("N2", new System.Globalization.CultureInfo("pt-BR"));

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);
                page.DefaultTextStyle(t => t.FontSize(11).FontColor("#1c2732"));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Fidellis").FontSize(22).Bold();
                            c.Item().Text("Recibo de Doação").FontColor("#64717e").FontSize(10);
                        });
                        row.ConstantItem(180).AlignRight().Column(c =>
                        {
                            c.Item().Text("Nº DO RECIBO").FontSize(8).FontColor("#64717e").LetterSpacing(0.1f);
                            c.Item().Text(r.Number).FontSize(15).SemiBold();
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#c8a24b");
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(10);
                    void Field(string label, string value)
                        => col.Item().Row(row =>
                        {
                            row.ConstantItem(180).Text(label).FontColor("#64717e");
                            row.RelativeItem().Text(value).SemiBold();
                        });

                    Field("Instituição / Unidade", r.OrganizationName ?? "—");
                    Field("Doador", r.DonorName);
                    Field("Documento", r.DonorDocument ?? "—");
                    Field("Data", r.IssuedAt.ToString("dd/MM/yyyy"));

                    col.Item().PaddingTop(12).Background("#f4f1ea").Padding(16).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("VALOR RECEBIDO").FontSize(10).FontColor("#64717e").LetterSpacing(0.1f);
                        row.ConstantItem(200).AlignRight().Text($"{Currency[0]} {brl}").FontSize(20).Bold();
                    });

                    col.Item().PaddingTop(16).Text(
                        "Recebemos a importância acima a título de doação. Este recibo comprova a contribuição para fins de prestação de contas.")
                        .FontColor("#64717e").LineHeight(1.6f);

                    col.Item().PaddingTop(48).AlignCenter().Column(c =>
                    {
                        c.Item().Width(260).LineHorizontal(1).LineColor("#9aa4ad");
                        c.Item().AlignCenter().Text(r.OrganizationName ?? "Instituição").FontColor("#64717e").FontSize(10);
                    });
                });

                page.Footer().AlignCenter().Text("Fidellis · prestação de contas do terceiro setor")
                    .FontSize(8).FontColor("#9aa4ad");
            });
        }).GeneratePdf();
    }
}
