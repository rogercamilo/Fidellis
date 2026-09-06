namespace Fidellis.Infrastructure.Messaging;

/// <summary>Contexto para renderizar as mensagens da régua.</summary>
public sealed record MessageContext(
    string DonorName,
    string? OrgName = null,
    decimal? Amount = null,
    string? ReceiptNumber = null);

public sealed record RenderedMessage(string Subject, string Body);

/// <summary>Templates da régua de relacionamento (puro/testável). Eventos bem-conhecidos.</summary>
public static class MessageTemplates
{
    public const string ThankYou = "thank_you";
    public const string PaymentFailed = "payment_failed";
    public const string PastDue = "past_due";
    public const string Reactivation = "reactivation";

    private static string Brl(decimal v) => v.ToString("C", new System.Globalization.CultureInfo("pt-BR"));

    public static RenderedMessage Render(string eventType, MessageContext ctx)
    {
        var name = string.IsNullOrWhiteSpace(ctx.DonorName) ? "amigo(a)" : ctx.DonorName;
        var org = ctx.OrgName ?? "nossa comunidade";

        return eventType switch
        {
            ThankYou => new RenderedMessage(
                "Recebemos sua doação — muito obrigado!",
                $"Olá, {name}!\n\nRecebemos sua doação de {Brl(ctx.Amount ?? 0)} para {org}. Muito obrigado pela sua generosidade — ela sustenta nossa missão." +
                (ctx.ReceiptNumber is { } r ? $"\n\nRecibo nº {r}." : string.Empty) +
                "\n\nCom gratidão,\nEquipe Fidellis"),

            PaymentFailed => new RenderedMessage(
                "Não conseguimos processar sua doação",
                $"Olá, {name}.\n\nTentamos processar sua doação recorrente para {org}, mas o pagamento não foi concluído. " +
                "Vamos tentar novamente nos próximos dias. Se preferir, você pode refazer a contribuição a qualquer momento.\n\nObrigado,\nEquipe Fidellis"),

            PastDue => new RenderedMessage(
                "Sentimos sua falta 🙏",
                $"Olá, {name}.\n\nSua contribuição recorrente para {org} está pausada por falta de confirmação de pagamento. " +
                "Adoraríamos continuar contando com seu apoio — retomar leva menos de um minuto.\n\nCom carinho,\nEquipe Fidellis"),

            Reactivation => new RenderedMessage(
                "Que tal voltar a apoiar nossa missão?",
                $"Olá, {name}!\n\nFaz um tempo desde sua última doação para {org}. Sua ajuda faz diferença — " +
                "que tal retomar sua contribuição? Ficaremos felizes em tê-lo(a) de volta.\n\nUm abraço,\nEquipe Fidellis"),

            _ => new RenderedMessage("Fidellis", $"Olá, {name}."),
        };
    }
}
