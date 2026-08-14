namespace CyberLens.Services.Storage;

public class FileSystemStorage(string rootPath) : IFileStorage
{
    public string ProviderName => "FileSystem";

    private string Resolve(string path) => Path.Combine(rootPath, StoragePath.Sanitize(path).Replace('/', Path.DirectorySeparatorChar));

    public async Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return StoragePath.Sanitize(path);
    }

    public Task<(Stream Stream, string ContentType)?> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        if (!File.Exists(full)) return Task.FromResult<(Stream, string)?>(null);
        Stream s = File.OpenRead(full);
        return Task.FromResult<(Stream, string)?>((s, StoragePath.GuessContentType(full)));
    }

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }
}
