using CyberLens.Models;

namespace CyberLens.Services.Storage;

/// <summary>
/// Singleton facade that delegates to the backend selected in settings.
/// Rebuilds the backend when configuration changes, so switching providers needs no restart.
/// </summary>
public class StorageService : IFileStorage
{
    private readonly AppSettingsService _settings;
    private readonly IWebHostEnvironment _env;
    private IFileStorage? _inner;
    private readonly object _gate = new();

    public StorageService(AppSettingsService settings, IWebHostEnvironment env)
    {
        _settings = settings;
        _env = env;
        settings.Changed += () => { lock (_gate) _inner = null; };
    }

    private IFileStorage Inner
    {
        get
        {
            lock (_gate)
            {
                if (_inner is not null) return _inner;
                var s = _settings.Current.Storage;
                _inner = s.Provider switch
                {
                    "AzureBlob" => new AzureBlobStorage(s.AzureBlobConnectionString, s.AzureBlobContainer),
                    "S3" => new S3Storage(s.S3AccessKey, s.S3SecretKey, s.S3Bucket, s.S3Region, null, "S3"),
                    "MinIO" => new S3Storage(s.MinioAccessKey, s.MinioSecretKey, s.MinioBucket, "us-east-1", s.MinioEndpoint, "MinIO"),
                    _ => new FileSystemStorage(Path.IsPathRooted(s.FileSystemRoot)
                        ? s.FileSystemRoot
                        : Path.Combine(_env.ContentRootPath, s.FileSystemRoot)),
                };
                return _inner;
            }
        }
    }

    public string ProviderName => Inner.ProviderName;

    public Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
        => Inner.SaveAsync(path, content, contentType, ct);

    public Task<(Stream Stream, string ContentType)?> OpenReadAsync(string path, CancellationToken ct = default)
        => Inner.OpenReadAsync(path, ct);

    public Task DeleteAsync(string path, CancellationToken ct = default)
        => Inner.DeleteAsync(path, ct);

    /// <summary>Save an upload under uploads/yyyy/MM with a unique name; returns the public /files URL.</summary>
    public async Task<(string Url, string StoragePathValue)> SaveUploadAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        var safeName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        var path = $"uploads/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}_{safeName}";
        var saved = await SaveAsync(path, content, StoragePath.GuessContentType(safeName), ct);
        return ($"/files/{saved}", saved);
    }
}
