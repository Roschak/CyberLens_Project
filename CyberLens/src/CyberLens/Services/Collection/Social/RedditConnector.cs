using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Reddit connector using the public JSON endpoints (no authentication required, just a
/// descriptive User-Agent). Fetches the newest posts from configured subreddits.
/// </summary>
public class RedditConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Reddit";
    public SourceKind Kind => SourceKind.Forum;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.Reddit.Enabled;
    public bool IsConfigured(AppConfig cfg) => true; // public API

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.Reddit;
        var http = httpFactory.CreateClient("web");
        var items = new List<CollectedItem>();
        foreach (var sub in c.Subreddits.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.reddit.com/r/{sub.Trim()}/new.json?limit={Math.Clamp(c.MaxPerSubreddit, 1, 25)}");
            req.Headers.Add("User-Agent", "CyberLens-OSINT/1.0 (media monitoring)");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) continue;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("children", out var children)) continue;

            foreach (var child in children.EnumerateArray())
            {
                var d = child.GetProperty("data");
                var title = Str(d, "title");
                var body = Str(d, "selftext");
                var text = string.IsNullOrWhiteSpace(body) ? title : $"{title}. {body}";
                if (text.Length < 8) continue;
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: $"Reddit r/{sub}",
                    Kind: Kind,
                    Author: Str(d, "author"),
                    AuthorHandle: "u/" + Str(d, "author"),
                    Title: title,
                    Content: text.Length > 4000 ? text[..4000] : text,
                    Url: "https://reddit.com" + Str(d, "permalink"),
                    PublishedAt: DateTimeOffset.FromUnixTimeSeconds((long)Num(d, "created_utc")).UtcDateTime,
                    Language: "en",
                    Likes: (int)Num(d, "ups"),
                    Comments: (int)Num(d, "num_comments"),
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static double Num(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
