using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Mastodon connector using public, unauthenticated hashtag timelines
/// (<c>/api/v1/timelines/tag/{hashtag}</c>). Real fediverse social data with no credentials.
/// </summary>
public class MastodonConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Mastodon";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.Mastodon.Enabled;
    public bool IsConfigured(AppConfig cfg) => !string.IsNullOrWhiteSpace(cfg.Social.Mastodon.Instance);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.Mastodon;
        var http = httpFactory.CreateClient("web");
        var baseUrl = c.Instance.TrimEnd('/');
        var items = new List<CollectedItem>();
        foreach (var tag in c.Hashtags.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var url = $"{baseUrl}/api/v1/timelines/tag/{Uri.EscapeDataString(tag.Trim().TrimStart('#'))}?limit={Math.Clamp(c.MaxPerHashtag, 1, 20)}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) continue;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

            foreach (var s in doc.RootElement.EnumerateArray())
            {
                var text = StripHtml(Str(s, "content"));
                if (text.Length < 8) continue;
                var acct = s.TryGetProperty("account", out var a) ? a : default;
                var display = acct.ValueKind == JsonValueKind.Object ? Str(acct, "display_name") : "";
                var uname = acct.ValueKind == JsonValueKind.Object ? Str(acct, "username") : "anon";
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: $"Mastodon #{tag.TrimStart('#')}",
                    Kind: Kind,
                    Author: string.IsNullOrWhiteSpace(display) ? uname : display,
                    AuthorHandle: "@" + uname,
                    Title: text.Length > 80 ? text[..77] + "..." : text,
                    Content: text.Length > 4000 ? text[..4000] : text,
                    Url: Str(s, "url"),
                    PublishedAt: DateTime.TryParse(Str(s, "created_at"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Language: Str(s, "language") is { Length: > 0 } lg ? lg : "en",
                    Likes: (int)Num(s, "favourites_count"),
                    Shares: (int)Num(s, "reblogs_count"),
                    Comments: (int)Num(s, "replies_count"),
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static double Num(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();
}
