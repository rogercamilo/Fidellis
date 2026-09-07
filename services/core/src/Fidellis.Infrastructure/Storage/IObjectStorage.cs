namespace Fidellis.Infrastructure.Storage;

/// <summary>
/// Armazenamento de objetos S3-compatível (Cloudflare R2 em prod; MinIO local em dev). Usado para
/// arquivar recibos em PDF de forma imutável. Implementação no-op quando o storage não está configurado.
/// </summary>
public interface IObjectStorage
{
    /// <summary>Indica se há um backend real configurado (falso = no-op).</summary>
    bool Enabled { get; }

    Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default);

    /// <summary>Baixa o objeto; <c>null</c> se não existir.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken ct = default);
}
