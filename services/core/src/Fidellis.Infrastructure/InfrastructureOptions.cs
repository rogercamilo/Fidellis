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
}
