using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Facebook connector using the official <b>Graph API</b> (page feed). Requires a Page access
/// token and one or more Page IDs set in Settings. (Graph API only exposes Pages you manage /
/// are permitted to read — personal profiles are not accessible.)
/// </summary>
public class FacebookConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Facebook";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.Facebook.Enabled;
    public bool IsConfigured(AppConfig cfg) =>
        !string.IsNullOrWhiteSpace(cfg.Social.Facebook.AccessToken) && cfg.Social.Facebook.PageIds.Count > 0;

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.Facebook;
        var http = httpFactory.CreateClient("web");
        var items = new List<CollectedItem>();
        foreach (var page in c.PageIds.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            var url = $"https://graph.facebook.com/v20.0/{page.Trim()}/posts" +
                      "?fields=message,created_time,permalink_url,shares,likes.summary(true),comments.summary(true)" +
                      $"&limit={Math.Clamp(c.MaxPerPage, 1, 25)}&access_token={c.AccessToken}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Facebook Graph HTTP {(int)resp.StatusCode}");
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var arr)) continue;

            foreach (var p in arr.EnumerateArray())
            {
                var text = Str(p, "message");
                if (text.Length < 8) continue;
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: $"Facebook Page {page}",
                    Kind: Kind,
                    Author: $"Page {page}",
                    AuthorHandle: page,
                    Title: text.Length > 80 ? text[..77] + "..." : text,
                    Content: text.Length > 4000 ? text[..4000] : text,
                    Url: Str(p, "permalink_url"),
                    PublishedAt: DateTime.TryParse(Str(p, "created_time"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                    Language: "id",
                    Likes: Summary(p, "likes"),
                    Comments: Summary(p, "comments"),
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static int Summary(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.TryGetProperty("summary", out var s) && s.TryGetProperty("total_count", out var n)
            ? n.GetInt32() : 0;
}
