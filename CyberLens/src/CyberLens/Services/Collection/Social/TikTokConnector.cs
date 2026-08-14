using System.Text;
using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// TikTok connector using the official <b>TikTok API</b>. TikTok requires approved developer
/// credentials (client key/secret) and OAuth; this connector performs the client-credentials
/// token exchange and video query when configured. Requires an approved TikTok developer app.
/// </summary>
public class TikTokConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "TikTok";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.TikTok.Enabled;
    public bool IsConfigured(AppConfig cfg) =>
        !string.IsNullOrWhiteSpace(cfg.Social.TikTok.ClientKey) && !string.IsNullOrWhiteSpace(cfg.Social.TikTok.ClientSecret);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.TikTok;
        var http = httpFactory.CreateClient("web");

        // 1. Client-credentials token
        using var tokReq = new HttpRequestMessage(HttpMethod.Post, "https://open.tiktokapis.com/v2/oauth/token/")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = c.ClientKey,
                ["client_secret"] = c.ClientSecret,
                ["grant_type"] = "client_credentials"
            })
        };
        using var tokResp = await http.SendAsync(tokReq, ct);
        if (!tokResp.IsSuccessStatusCode)
            throw new InvalidOperationException($"TikTok OAuth HTTP {(int)tokResp.StatusCode}");
        using var tokDoc = JsonDocument.Parse(await tokResp.Content.ReadAsStringAsync(ct));
        if (!tokDoc.RootElement.TryGetProperty("access_token", out var at))
            throw new InvalidOperationException("TikTok OAuth: no access_token in response");
        var token = at.GetString();

        // 2. Video query (Research API). Requires an approved research/display app.
        var items = new List<CollectedItem>();
        foreach (var term in c.SearchTerms.Where(t => !string.IsNullOrWhiteSpace(t)))
        {
            var body = JsonSerializer.Serialize(new
            {
                query = new { and = new[] { new { operation = "IN", field_name = "keyword", field_values = new[] { term.Trim() } } } },
                max_count = Math.Clamp(c.MaxResults, 1, 100)
            });
            using var req = new HttpRequestMessage(HttpMethod.Post,
                "https://open.tiktokapis.com/v2/research/video/query/?fields=id,video_description,create_time,like_count,comment_count,share_count,username")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            req.Headers.Add("Authorization", $"Bearer {token}");
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) continue; // research endpoint may be gated for the app
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("videos", out var vids)) continue;

            foreach (var v in vids.EnumerateArray())
            {
                var text = Str(v, "video_description");
                if (text.Length < 8) continue;
                var geo = SimpleGeocoder.Locate(text);
                items.Add(new CollectedItem(
                    SourceName: $"TikTok — {term}",
                    Kind: Kind,
                    Author: Str(v, "username"),
                    AuthorHandle: "@" + Str(v, "username"),
                    Title: text.Length > 80 ? text[..77] + "..." : text,
                    Content: text,
                    Url: $"https://tiktok.com/@{Str(v, "username")}/video/{Str(v, "id")}",
                    PublishedAt: DateTimeOffset.FromUnixTimeSeconds((long)Num(v, "create_time")).UtcDateTime,
                    Language: "id",
                    Likes: (int)Num(v, "like_count"),
                    Shares: (int)Num(v, "share_count"),
                    Comments: (int)Num(v, "comment_count"),
                    Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
            }
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : "";
    private static double Num(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
}
