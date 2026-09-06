namespace Fidellis.Infrastructure;

/// <summary>Configuração de infraestrutura (conexões) do core.</summary>
public sealed class InfrastructureOptions
{
    /// <summary>Connection string Npgsql para o Postgres. Ex.: <c>Host=...;Database=...;Username=...;Password=...</c></summary>
    public required string ConnectionString { get; init; }

    /// <summary>Endpoint do Redis no formato do StackExchange (<c>host:port</c>). Opcional.</summary>
    public string? RedisConnection { get; init; }

    /// <summary>Secret key do Pagar.me (<c>sk_...</c>) usada como usuário no Basic auth da API.</summary>
    public string? PagarmeApiKey { get; init; }

    /// <summary>Base URL da API Core do Pagar.me. Padrão: <c>https://api.pagar.me/core/v5</c>.</summary>
    public string PagarmeBaseUrl { get; init; } = "https://api.pagar.me/core/v5";

    /// <summary>Usuário do Basic auth configurado na URL de webhook do Pagar.me (validação do receptor).</summary>
    public string? PagarmeWebhookUser { get; init; }

    /// <summary>Senha do Basic auth configurado na URL de webhook do Pagar.me.</summary>
    public string? PagarmeWebhookPassword { get; init; }

    /// <summary>
    /// Segredo p/ validar a assinatura HMAC-SHA256 do webhook do Pagar.me sobre o corpo bruto
    /// (RF-FIN-001). Quando definido, tem precedência sobre o Basic auth; vazio → cai no Basic auth.
    /// </summary>
    public string? PagarmeWebhookSignatureSecret { get; init; }

    /// <summary>API key do Resend p/ envio de e-mail. Vazia → envio é ignorado (log).</summary>
    public string? ResendApiKey { get; init; }

    /// <summary>Remetente dos e-mails (ex.: <c>Fidellis &lt;nao-responda@dominio.com&gt;</c>).</summary>
    public string MailFrom { get; init; } = "Fidellis <onboarding@resend.dev>";

    /// <summary>Dias sem doação para considerar um doador inativo (reativação).</summary>
    public int ReactivationDays { get; init; } = 90;

    /// <summary>Segredo da aplicação (= JWT_SECRET) para assinar o link mágico do doador.</summary>
    public string AppSecret { get; init; } = "change-me-in-prod-please-use-a-long-random-secret";

    /// <summary>URL base do web para montar links (ex.: link mágico no e-mail).</summary>
    public string AppBaseUrl { get; init; } = "http://localhost:3000";
}
