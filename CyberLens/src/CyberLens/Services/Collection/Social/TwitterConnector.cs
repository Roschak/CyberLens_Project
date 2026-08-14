using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Twitter / X connector using the official <b>X API v2</b> recent-search endpoint
/// (<c>/2/tweets/search/recent</c>). Requires an App Bearer token set in Settings.
/// </summary>
public class TwitterConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Twitter/X";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.Twitter.Enabled;
    public bool IsConfigured(AppConfig cfg) => !string.IsNullOrWhiteSpace(cfg.Social.Twitter.BearerToken);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.Twitter;
        var http = httpFactory.CreateClient("web");
        var items = new List<CollectedItem>();
        foreach (var term in c.SearchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var url = "https://api.twitter.com/2/tweets/search/recent" +
                      $"?query={Uri.EscapeDataString(term.Trim() + " -is:retweet")}" +
                      $"&max_results={Math.Clamp(c.MaxResults, 10, 100)}" +
                      "&tweet.fields=created_at,public_metrics,lang,geo,author_id";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("Authorization", $"Bearer {c.BearerToken}");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"X API HTTP {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var arr)) continue;

            foreach (var t in arr.EnumerateArray())
            {
                var text = Str(t, "text");
                if (text.Length < 8) continue;
                var m = t.TryGetProperty("public_metrics", out var pm) ? pm : default;
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: $"Twitter/X — {term}",
                    Kind: Kind,
                    Author: Str(t, "author_id"),
                    AuthorHandle: "@" + Str(t, "author_id"),
                    Title: text.Length > 80 ? text[..77] + "..." : text,
                    Content: text,
                    Url: $"https://twitter.com/i/web/status/{Str(t, "id")}",
                    PublishedAt: DateTime.TryParse(Str(t, "created_at"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Language: Str(t, "lang") is { Length: > 0 } lg ? lg : "en",
                    Likes: (int)Num(m, "like_count"),
                    Shares: (int)Num(m, "retweet_count"),
                    Comments: (int)Num(m, "reply_count"),
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number
            ? v.ToString() : "";
    private static double Num(JsonElement e, string p) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
