using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>A normalized item fetched from an external source before it becomes a <see cref="Post"/>.</summary>
public record CollectedItem(
    string SourceName,
    SourceKind Kind,
    string Author,
    string AuthorHandle,
    string Title,
    string Content,
    string Url,
    DateTime PublishedAt,
    string Language = "id",
    int Likes = 0,
    int Shares = 0,
    int Comments = 0,
    double? Lat = null,
    double? Lon = null,
    string? Location = null,
    (MediaKind Kind, string Url)? Media = null);

/// <summary>
/// A pluggable crawler for one external platform (social media, forum, etc.).
/// Implementations use official APIs/SDKs where possible; those needing credentials
/// read them from <see cref="AppConfig"/> and report themselves unconfigured until set.
/// </summary>
public interface ISocialConnector
{
    /// <summary>Display name shown in the crawler log (e.g. "YouTube", "Twitter/X").</summary>
    string Platform { get; }

    /// <summary>The kind of source this connector produces.</summary>
    SourceKind Kind { get; }

    /// <summary>True when the connector is enabled and has the credentials it needs.</summary>
    bool IsConfigured(AppConfig cfg);

    /// <summary>Whether the operator has switched this connector on.</summary>
    bool IsEnabled(AppConfig cfg);

    /// <summary>Fetch the latest items. Should throw on hard failures so the run is logged as failed.</summary>
    Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct);
}
