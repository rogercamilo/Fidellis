namespace Fidellis.Infrastructure.Storage;

/// <summary>
/// Storage no-op para dev/CI sem R2/MinIO: nunca arquiva e nunca acha nada, forçando o caminho de
/// geração sob demanda do PDF. Mantém o resto do código agnóstico à existência de storage.
/// </summary>
public sealed class NullObjectStorage : IObjectStorage
{
    public bool Enabled => false;

    public Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);
}
