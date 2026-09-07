using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;

namespace Fidellis.Infrastructure.Storage;

/// <summary>
/// Armazenamento S3-compatível (Cloudflare R2 em prod; MinIO em dev). O mesmo código serve os dois —
/// muda só o <c>StorageEndpoint</c>/credenciais. Usa path-style (exigido pelo MinIO e aceito pelo R2)
/// e região fixa <c>auto</c> (padrão do R2).
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly ILogger<S3ObjectStorage> _logger;

    public bool Enabled => true;

    public S3ObjectStorage(InfrastructureOptions options, ILogger<S3ObjectStorage> logger)
    {
        _logger = logger;
        _bucket = options.StorageBucket!;
        var config = new AmazonS3Config
        {
            ServiceURL = options.StorageEndpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
        };
        _client = new AmazonS3Client(options.StorageAccessKey, options.StorageSecret, config);
    }

    public async Task PutAsync(string key, byte[] content, string contentType, CancellationToken ct = default)
    {
        using var stream = new MemoryStream(content);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
        }, ct);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken ct = default)
    {
        try
        {
            using var res = await _client.GetObjectAsync(_bucket, key, ct);
            using var ms = new MemoryStream();
            await res.ResponseStream.CopyToAsync(ms, ct);
            return ms.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Garante o bucket (best-effort). No R2 o bucket é pré-criado; erros são ignorados.</summary>
    public async Task EnsureBucketAsync(CancellationToken ct = default)
    {
        try
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_client, _bucket);
            if (!exists) await _client.PutBucketAsync(_bucket, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível garantir o bucket {Bucket} (seguindo mesmo assim).", _bucket);
        }
    }
}
