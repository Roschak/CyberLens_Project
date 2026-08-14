using System.Diagnostics;
using System.ServiceModel.Syndication;
using System.Xml;
using CyberLens.Data;
using CyberLens.Models;
using CyberLens.Services.Analysis;
using CyberLens.Services.Collection.Social;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services.Collection;

/// <summary>
/// Performs one collection pass across every configured connector and records a
/// <see cref="CrawlRun"/> per connector (the crawler activity log). Shared by the scheduled
/// <see cref="CrawlerService"/> and the manual "Crawl sekarang" button.
///
/// Connectors: real RSS/Atom feeds, real social/forum APIs (Reddit, Mastodon, YouTube,
/// Twitter/X, Facebook, Threads, TikTok — those needing keys read them from settings), and
/// an optional simulated social stream for demo. Every item is normalized, de-duplicated,
/// sentiment-scored, auto-classified and geocoded.
/// </summary>
public class CollectorService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    NotificationBus bus,
    IHttpClientFactory httpFactory,
    IEnumerable<ISocialConnector> connectors,
    CrawlerStatusService status,
    ILogger<CollectorService> logger)
{
    private readonly Random _rng = new();

    public async Task<int> RunOnceAsync(AppConfig cfg, string trigger = "Scheduled", CancellationToken ct = default)
    {
        var crawler = cfg.Crawler;
        status.BeginPass(trigger);
        var total = 0;
        try
        {
            // --- Real RSS/Atom feeds ---
            foreach (var feed in crawler.RssFeeds.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                var host = TryHost(feed);
                status.SetConnector($"RSS: {host}");
                total += await RunConnectorAsync($"RSS: {host}", SourceKind.News, trigger,
                    () => FetchRssAsync(feed.Trim(), ct), ct);
            }

            // --- Social / forum connectors ---
            foreach (var connector in connectors)
            {
                if (!connector.IsEnabled(cfg)) continue;
                if (connector.IsConfigured(cfg))
                {
                    status.SetConnector(connector.Platform);
                    total += await RunConnectorAsync(connector.Platform, connector.Kind, trigger,
                        () => connector.FetchAsync(cfg, ct), ct);
                }
                else if (trigger == "Manual")
                {
                    // Surface "needs credentials" on manual runs, so operators see it in the log.
                    await LogRunAsync(connector.Platform, connector.Kind, trigger, DateTime.UtcNow,
                        TimeSpan.Zero, 0, 0, 0, false, "Kredensial belum diatur (isi di Pengaturan)");
                }
            }

            // --- Simulated stream (demo) ---
            if (crawler.SimulateSocialStreams)
            {
                status.SetConnector("Simulator");
                total += await RunSimulatorAsync(crawler.DarkWebMonitoring, trigger, ct);
            }
        }
        finally
        {
            status.EndPass(total);
        }
        return total;
    }

    // ---- Per-connector execution + logging ----
    private async Task<int> RunConnectorAsync(string name, SourceKind kind, string trigger,
        Func<Task<IReadOnlyList<CollectedItem>>> fetch, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int found = 0, added = 0, dup = 0; var ok = true; string? err = null;
        try
        {
            var items = await fetch();
            found = items.Count;
            (added, dup) = await StoreItemsAsync(items, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { ok = false; err = ex.Message; logger.LogWarning(ex, "Connector {Name} failed", name); }
        sw.Stop();
        await LogRunAsync(name, kind, trigger, started, sw.Elapsed, found, added, dup, ok, err);
        return added;
    }

    // ---- Store collected items (dedup, sentiment, classify, geocode, persist, publish) ----
    private async Task<(int Added, int Dup)> StoreItemsAsync(IReadOnlyList<CollectedItem> items, CancellationToken ct)
    {
        if (items.Count == 0) return (0, 0);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var categories = await db.Categories.ToListAsync(ct);
        var sourceCache = new Dictionary<string, Source>();
        var seen = new HashSet<string>();
        var newPosts = new List<Post>();
        var dup = 0;

        foreach (var it in items)
        {
            var hash = SampleContent.Sha256(!string.IsNullOrWhiteSpace(it.Url) ? it.Url : it.Content + it.SourceName);
            if (!seen.Add(hash)) { dup++; continue; }
            if (await db.Posts.AnyAsync(p => p.Hash == hash, ct)) { dup++; continue; }

            // resolve/create source
            if (!sourceCache.TryGetValue(it.SourceName, out var source))
            {
                source = await db.Sources.FirstOrDefaultAsync(s => s.Name == it.SourceName, ct);
                if (source is null)
                {
                    source = new Source { Name = it.SourceName, Kind = it.Kind, Url = it.Url, Country = "Global", TrustScore = 0.5 };
                    db.Sources.Add(source);
                }
                sourceCache[it.SourceName] = source;
            }

            var (score, label) = SentimentAnalyzer.Analyze(it.Content);
            var catName = TopicClassifier.Classify(it.Content);
            var category = catName is null ? null : categories.FirstOrDefault(c => c.Name == catName);

            var post = new Post
            {
                Source = source,
                Category = category,
                Author = it.Author,
                AuthorHandle = it.AuthorHandle,
                Title = it.Title.Length > 250 ? it.Title[..250] : it.Title,
                Content = it.Content,
                Language = it.Language,
                Url = it.Url,
                PublishedAt = it.PublishedAt == default ? DateTime.UtcNow : it.PublishedAt,
                CollectedAt = DateTime.UtcNow,
                SentimentScore = score,
                SentimentLabel = label,
                Likes = it.Likes, Shares = it.Shares, Comments = it.Comments,
                Latitude = it.Lat, Longitude = it.Lon, LocationName = it.Location,
                Tags = SampleContent.ExtractTags(it.Content),
                Hash = hash,
            };
            if (it.Media is { } m)
                post.Media.Add(new PostMedia { Kind = m.Kind, Url = m.Url, Caption = "Lampiran" });
            db.Posts.Add(post);
            newPosts.Add(post);
        }

        if (newPosts.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            foreach (var p in newPosts) bus.PublishPost(p);
        }
        return (newPosts.Count, dup);
    }

    private async Task<int> RunSimulatorAsync(bool includeDarkWeb, string trigger, CancellationToken ct)
    {
        var started = DateTime.UtcNow; var sw = Stopwatch.StartNew();
        int found = 0, added = 0; var ok = true; string? err = null;
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var sources = await db.Sources.Where(s => s.IsActive).ToListAsync(ct);
            var categories = await db.Categories.ToListAsync(ct);
            if (!includeDarkWeb) sources = sources.Where(s => s.Kind != SourceKind.DarkWeb).ToList();
            if (sources.Count > 0 && categories.Count > 0)
            {
                var burst = _rng.Next(1, 4);
                found = burst;
                for (var i = 0; i < burst; i++)
                {
                    var source = sources[_rng.Next(sources.Count)];
                    var post = SampleContent.BuildPost(_rng, source, categories, DateTime.UtcNow.AddSeconds(-_rng.Next(0, 90)));
                    post.Source = null;
                    if (await db.Posts.AnyAsync(p => p.Hash == post.Hash, ct)) continue;
                    db.Posts.Add(post);
                    await db.SaveChangesAsync(ct);
                    post.Source = source;
                    bus.PublishPost(post);
                    added++;
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { ok = false; err = ex.Message; }
        sw.Stop();
        await LogRunAsync("Simulator (demo)", SourceKind.SocialMedia, trigger, started, sw.Elapsed, found, added, found - added, ok, err);
        return added;
    }

    private async Task LogRunAsync(string connector, SourceKind kind, string trigger, DateTime started,
        TimeSpan duration, int found, int added, int dup, bool success, string? error)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            db.CrawlRuns.Add(new CrawlRun
            {
                Connector = connector, Kind = kind, Trigger = trigger,
                StartedAt = started, FinishedAt = started + duration, DurationMs = (int)duration.TotalMilliseconds,
                ItemsFound = found, ItemsAdded = added, ItemsDuplicate = dup, Success = success,
                Error = error is { Length: > 512 } ? error[..512] : error
            });
            await db.SaveChangesAsync();
        }
        catch { /* logging must never break collection */ }
    }

    // ---- RSS fetch → CollectedItems ----
    private async Task<IReadOnlyList<CollectedItem>> FetchRssAsync(string feedUrl, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("crawler");
        using var resp = await http.GetAsync(feedUrl, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        var feed = SyndicationFeed.Load(reader);
        if (feed is null) return Array.Empty<CollectedItem>();
        var host = TryHost(feedUrl);
        var name = feed.Title?.Text ?? host;

        var items = new List<CollectedItem>();
        foreach (var item in feed.Items.Take(25))
        {
            var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? "";
            var title = item.Title?.Text ?? "";
            var summary = StripHtml(item.Summary?.Text ?? "");
            var text = $"{title}. {summary}".Trim();
            if (text.Length < 10) continue;
            var geo = SimpleGeocoder.Locate(text);
            items.Add(new CollectedItem(
                SourceName: name, Kind: SourceKind.News,
                Author: item.Authors.FirstOrDefault()?.Name ?? name,
                AuthorHandle: host,
                Title: title, Content: text.Length > 4000 ? text[..4000] : text,
                Url: link,
                PublishedAt: item.PublishDate.UtcDateTime == default ? DateTime.UtcNow : item.PublishDate.UtcDateTime,
                Language: title.Any(c => c > 127) || summary.Contains(" yang ") ? "id" : "en",
                Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
        }
        return items;
    }

    private static string TryHost(string url) { try { return new Uri(url).Host; } catch { return url; } }
    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();
}
