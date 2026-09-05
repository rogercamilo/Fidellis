namespace Fidellis.Infrastructure;

/// <summary>Configuração de infraestrutura (conexões) do core.</summary>
public sealed class InfrastructureOptions
{
    /// <summary>Connection string Npgsql para o Postgres. Ex.: <c>Host=...;Database=...;Username=...;Password=...</c></summary>
    public required string ConnectionString { get; init; }

    /// <summary>Endpoint do Redis no formato do StackExchange (<c>host:port</c>). Opcional.</summary>
    public string? RedisConnection { get; init; }
}
