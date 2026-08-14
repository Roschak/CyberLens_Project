using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// YouTube connector using the official <b>YouTube Data API v3</b> (<c>search.list</c>).
/// Requires an API key (Google Cloud console) set in Settings.
/// </summary>
public class YouTubeConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "YouTube";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.YouTube.Enabled;
    public bool IsConfigured(AppConfig cfg) => !string.IsNullOrWhiteSpace(cfg.Social.YouTube.ApiKey);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.YouTube;
        var http = httpFactory.CreateClient("web");
        var items = new List<CollectedItem>();
        foreach (var term in c.SearchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var url = "https://www.googleapis.com/youtube/v3/search" +
                      $"?part=snippet&type=video&order=date&maxResults={Math.Clamp(c.MaxResults, 1, 25)}" +
                      $"&q={Uri.EscapeDataString(term.Trim())}&key={c.ApiKey}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"YouTube API HTTP {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("items", out var arr)) continue;

            foreach (var it in arr.EnumerateArray())
            {
                if (!it.TryGetProperty("snippet", out var sn)) continue;
                var vid = it.TryGetProperty("id", out var id) && id.TryGetProperty("videoId", out var v) ? v.GetString() : null;
                var title = Str(sn, "title");
                var desc = Str(sn, "description");
                var text = $"{title}. {desc}".Trim();
                if (text.Length < 8) continue;
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: "YouTube",
                    Kind: Kind,
                    Author: Str(sn, "channelTitle"),
                    AuthorHandle: "@" + Str(sn, "channelTitle").Replace(" ", ""),
                    Title: title,
                    Content: text.Length > 4000 ? text[..4000] : text,
                    Url: vid is null ? "https://youtube.com" : $"https://youtube.com/watch?v={vid}",
                    PublishedAt: DateTime.TryParse(Str(sn, "publishedAt"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Language: "id",
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name,
                    Media: vid is null ? null : (MediaKind.Video, $"https://youtube.com/watch?v={vid}")));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
