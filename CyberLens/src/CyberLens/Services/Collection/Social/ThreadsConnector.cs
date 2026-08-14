using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Threads connector using the official <b>Threads Graph API</b>
/// (<c>graph.threads.net/{user}/threads</c>). Requires a Threads access token set in Settings.
/// </summary>
public class ThreadsConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Threads";
    public SourceKind Kind => SourceKind.SocialMedia;

    public bool IsEnabled(AppConfig cfg) => cfg.Social.Threads.Enabled;
    public bool IsConfigured(AppConfig cfg) => !string.IsNullOrWhiteSpace(cfg.Social.Threads.AccessToken);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.Social.Threads;
        var http = httpFactory.CreateClient("web");
        var url = $"https://graph.threads.net/v1.0/{c.UserId}/threads" +
                  "?fields=text,permalink,timestamp,username" +
                  $"&limit={Math.Clamp(c.MaxResults, 1, 25)}&access_token={c.AccessToken}";
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Threads API HTTP {(int)resp.StatusCode}");
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var arr)) return Array.Empty<CollectedItem>();

        var items = new List<CollectedItem>();
        foreach (var t in arr.EnumerateArray())
        {
            var text = Str(t, "text");
            if (text.Length < 8) continue;
            var geo = SimpleGeocoder.Locate(text);
            items.Add(new CollectedItem(
                SourceName: "Threads",
                Kind: Kind,
                Author: Str(t, "username"),
                AuthorHandle: "@" + Str(t, "username"),
                Title: text.Length > 80 ? text[..77] + "..." : text,
                Content: text.Length > 4000 ? text[..4000] : text,
                Url: Str(t, "permalink"),
                PublishedAt: DateTime.TryParse(Str(t, "timestamp"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                Language: "id",
                Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
        }
        return items;
    }

    private static string Str(JsonElement e, string p) =>
        e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
