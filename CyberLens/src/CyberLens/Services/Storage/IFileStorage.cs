namespace CyberLens.Services.Storage;

/// <summary>
/// Storage abstraction. Paths are relative (e.g. "uploads/2026/07/photo.png");
/// files are always served to browsers through the /files/{path} endpoint regardless of backend.
/// </summary>
public interface IFileStorage
{
    string ProviderName { get; }
    Task<string> SaveAsync(string path, Stream content, string contentType, CancellationToken ct = default);
    Task<(Stream Stream, string ContentType)?> OpenReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

public static class StoragePath
{
    public static string Sanitize(string path)
    {
        var clean = path.Replace('\\', '/').TrimStart('/');
        if (clean.Contains("..")) throw new InvalidOperationException("Invalid storage path.");
        return clean;
    }

    public static string GuessContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".gif" => "image/gif",
        ".webp" => "image/webp", ".svg" => "image/svg+xml", ".pdf" => "application/pdf",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".txt" => "text/plain", ".md" => "text/markdown", ".csv" => "text/csv",
        ".mp4" => "video/mp4", ".mp3" => "audio/mpeg", ".wav" => "audio/wav",
        ".json" => "application/json", ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
