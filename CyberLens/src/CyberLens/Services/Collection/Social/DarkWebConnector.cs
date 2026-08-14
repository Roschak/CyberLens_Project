using System.Net;
using System.Text.Json;
using CyberLens.Data;
using CyberLens.Models;

namespace CyberLens.Services.Collection.Social;

/// <summary>
/// Real dark-web monitoring connector.
/// - <b>Tor</b>: fetches configured <c>.onion</c> pages through a SOCKS5 proxy
///   (<see cref="SocketsHttpHandler"/> + <see cref="WebProxy"/> with a <c>socks5://</c> URI — a
///   running Tor client is required, e.g. <c>tor</c> on 127.0.0.1:9050).
/// - <b>Threat intel</b>: pulls a clearnet JSON feed of leaks/pastes using an API key.
/// Both sources are optional and disabled until configured in Settings.
/// </summary>
public class DarkWebConnector(IHttpClientFactory httpFactory) : ISocialConnector
{
    public string Platform => "Dark Web";
    public SourceKind Kind => SourceKind.DarkWeb;

    public bool IsEnabled(AppConfig cfg) => cfg.DarkWeb.Enabled;
    public bool IsConfigured(AppConfig cfg) =>
        (cfg.DarkWeb.OnionUrls.Count > 0 && !string.IsNullOrWhiteSpace(cfg.DarkWeb.TorProxy)) ||
        !string.IsNullOrWhiteSpace(cfg.DarkWeb.ThreatIntelApiUrl);

    public async Task<IReadOnlyList<CollectedItem>> FetchAsync(AppConfig cfg, CancellationToken ct)
    {
        var c = cfg.DarkWeb;
        var items = new List<CollectedItem>();

        // ---- 1. .onion pages via Tor SOCKS5 proxy ----
        if (c.OnionUrls.Count > 0 && !string.IsNullOrWhiteSpace(c.TorProxy))
        {
            using var handler = new SocketsHttpHandler { Proxy = new WebProxy(c.TorProxy), UseProxy = true };
            using var tor = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(40) };
            tor.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (CyberLens OSINT)");
            foreach (var onion in c.OnionUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                var html = await tor.GetStringAsync(onion.Trim(), ct);
                var text = StripHtml(html);
                if (text.Length < 20) continue;
                var host = TryHost(onion);
                foreach (var chunk in Chunk(text, c.MaxPerSource))
                {
                    var geo = SimpleGeocoder.Locate(chunk);
                    items.Add(new CollectedItem(
                        SourceName: $"Onion: {host}", Kind: Kind,
                        Author: $"anon_{Math.Abs(chunk.GetHashCode()) % 9000 + 1000}",
                        AuthorHandle: "@onion",
                        Title: chunk.Length > 80 ? chunk[..77] + "..." : chunk,
                        Content: chunk, Url: onion.Trim(),
                        PublishedAt: DateTime.UtcNow, Language: "en",
                        Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
                }
            }
        }

        // ---- 2. Threat-intel JSON feed (clearnet, API key) ----
        if (!string.IsNullOrWhiteSpace(c.ThreatIntelApiUrl))
        {
            var http = httpFactory.CreateClient("web");
            using var req = new HttpRequestMessage(HttpMethod.Get, c.ThreatIntelApiUrl);
            if (!string.IsNullOrWhiteSpace(c.ThreatIntelApiKey))
            {
                req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {c.ThreatIntelApiKey}");
                req.Headers.TryAddWithoutValidation("x-api-key", c.ThreatIntelApiKey);
            }
            using var resp = await http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var arr = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement
                : doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array ? d
                : doc.RootElement.TryGetProperty("results", out var r) && r.ValueKind == JsonValueKind.Array ? r
                : default;
            if (arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray().Take(c.MaxPerSource))
                {
                    var title = First(e, "title", "name", "headline");
                    var content = First(e, "content", "description", "summary", "text", "body");
                    var text = string.IsNullOrWhiteSpace(content) ? title : $"{title}. {content}";
                    if (text.Length < 8) continue;
                    var geo = SimpleGeocoder.Locate(text);
                    items.Add(new CollectedItem(
                        SourceName: "Threat Intel Feed", Kind: Kind,
                        Author: First(e, "actor", "source", "author"),
                        AuthorHandle: "@threatintel",
                        Title: title.Length > 0 ? title : (text.Length > 80 ? text[..77] + "..." : text),
                        Content: text.Length > 4000 ? text[..4000] : text,
                        Url: First(e, "url", "link", "reference"),
                        PublishedAt: DateTime.TryParse(First(e, "date", "published", "created_at", "timestamp"), out var dt) ? dt.ToUniversalTime() : DateTime.UtcNow,
                        Language: "en",
                        Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
                }
            }
        }

        return items;
    }

    private static IEnumerable<string> Chunk(string text, int max)
    {
        // Split long scraped pages into paragraph-like items.
        var parts = text.Split(new[] { ". ", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length >= 20).Take(max);
        return parts;
    }

    private static string First(JsonElement e, params string[] props)
    {
        foreach (var p in props)
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? "";
        return "";
    }
    private static string TryHost(string url) { try { return new Uri(url).Host; } catch { return url; } }
    private static string StripHtml(string html)
    {
        html = System.Text.RegularExpressions.Regex.Replace(html, "<script.*?</script>", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, "<style.*?</style>", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        html = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        return System.Text.RegularExpressions.Regex.Replace(System.Net.WebUtility.HtmlDecode(html), @"\s+", " ").Trim();
    }
}
