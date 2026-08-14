using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace CyberLens.Services.Storage;

public class AzureBlobStorage(string connectionString, string container) : IFileStorage
{
    public string ProviderName => "AzureBlob";
    private BlobContainerClient? _client;

    private async Task<BlobContainerClient> GetClientAsync(CancellationToken ct)
    {
        if (_client is null)
        {
            _client = new BlobContainerClient(connectionString, container);
            await _client.CreateIfNotExistsAsync(cancellationToken: ct);
        }
        return _client;
    }

    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var clean = StoragePath.Sanitize(path);
        var client = await GetClientAsync(ct);
        await client.GetBlobClient(clean).UploadAsync(content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        return clean;
    }

    public async Task<(Stream Stream, string ContentType)?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        var blob = client.GetBlobClient(StoragePath.Sanitize(path));
        if (!await blob.ExistsAsync(ct)) return null;
        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        var stream = await blob.OpenReadAsync(cancellationToken: ct);
        return (stream, props.Value.ContentType ?? StoragePath.GuessContentType(path));
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var client = await GetClientAsync(ct);
        await client.DeleteBlobIfExistsAsync(StoragePath.Sanitize(path), cancellationToken: ct);
    }
}
