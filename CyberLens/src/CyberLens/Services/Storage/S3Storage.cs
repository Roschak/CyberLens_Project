using Amazon.S3;
using Amazon.S3.Model;

namespace CyberLens.Services.Storage;

/// <summary>Amazon S3 backend; also serves MinIO via a custom ServiceURL with path-style addressing.</summary>
public class S3Storage : IFileStorage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private bool _bucketChecked;

    public string ProviderName { get; }

    public S3Storage(string accessKey, string secretKey, string bucket, string region, string? serviceUrl, string providerName)
    {
        ProviderName = providerName;
        _bucket = bucket;
        var cfg = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            cfg.ServiceURL = serviceUrl;      // MinIO or S3-compatible endpoint
            cfg.ForcePathStyle = true;
        }
        else
        {
            cfg.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region);
        }
        _client = new AmazonS3Client(accessKey, secretKey, cfg);
    }

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (_bucketChecked) return;
        try { await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct); }
        catch (AmazonS3Exception) { /* already exists / owned */ }
        _bucketChecked = true;
    }

    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var clean = StoragePath.Sanitize(path);
        await EnsureBucketAsync(ct);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket, Key = clean, InputStream = content, ContentType = contentType
        }, ct);
        return clean;
    }

    public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        try
        {
            var resp = await _client.GetObjectAsync(_bucket, StoragePath.Sanitize(path), ct);
            var ms = new MemoryStream();
            await resp.ResponseStream.CopyToAsync(ms, ct);
            ms.Position = 0;
            return (ms, resp.Headers.ContentType ?? StoragePath.GuessContentType(path));
        }
        catch (AmazonS3Exception e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
        => _client.DeleteObjectAsync(_bucket, StoragePath.Sanitize(path), ct);
}
