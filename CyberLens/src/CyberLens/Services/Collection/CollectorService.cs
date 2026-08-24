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
/// Twitter/X, Facebook, Threads, TikTok — those needing keys read them from settings).
/// Every item is normalized, de-duplicated, sentiment-scored, auto-classified and geocoded.
/// </summary>
public class CollectorService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    NotificationBus bus,
    IHttpClientFactory httpFactory,
    IEnumerable<ISocialConnector> connectors,
    CrawlerStatusService status,
    ILogger<CollectorService> logger)
{
    private readonly int _retryCount = 2;

    // Minimal keyword relevance check: RSS feed items yang diambil via keyword search
    // harus memang mengandung keyword tersebut. Mencegah Google News mengembalikan
    // hasil yang tidak relevan (iklan, hasil fuzzy, dll).
    private static bool IsKeywordRelevant(string title, string content, string keyword)
    {
        var combined = $"{title} {content}";
        return combined.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
    public async Task<int> RunOnceAsync(AppConfig cfg, string trigger = "Scheduled", CancellationToken ct = default)
    {
        var crawler = cfg.Crawler;
        var mode = crawler.Mode;
        var maxPerFeed = Math.Max(1, crawler.MaxItemsPerFeed);      // batas item per feed/connector
        var maxPerKw = Math.Max(1, crawler.MaxItemsPerKeyword);     // batas item per keyword search
        var maxContent = Math.Max(500, crawler.MaxContentLength);    // potong konten agar hemat ruang & token AI
        var maxNew = Math.Max(10, crawler.MaxNewPostsPerCycle);      // budget post BARU per siklus
        var kwBudget = Math.Max(5, crawler.MaxNewPostsPerCycleKeyword); // budget khusus keyword
        var maxConnectors = Math.Max(5, crawler.MaxConnectorsPerCycle);
        var perConnectorTimeout = TimeSpan.FromSeconds(Math.Clamp(crawler.RequestTimeoutSeconds, 10, 120));
        status.BeginPass(trigger);
        var total = 0;
        var connectorCount = 0;
        try
        {
            // --- Kata kunci pantauan: cari berita Indonesia untuk setiap keyword aktif ---
            // Diproses PERTAMA agar budget post baru tidak habis oleh feed umum — keyword
            // adalah permintaan eksplisit operator dan harus selalu mendapatkan jatahnya.
            var keywords = await GetActiveKeywordsAsync(ct);
            var kwTotal = 0;
            foreach (var kw in keywords)
            {
                if (kwTotal >= kwBudget || total >= maxNew || ++connectorCount > maxConnectors) break;
                var url = $"https://news.google.com/rss/search?q={Uri.EscapeDataString(kw)}&hl=id&gl=ID&ceid=ID:id";
                status.SetConnector($"Keyword: {kw}");
                var added = await RunConnectorAsync($"Keyword: {kw}", SourceKind.News, trigger, mode,
                    () => FetchRssFilteredAsync(url, maxPerKw, kw, ct), ct, maxContent, perConnectorTimeout);
                kwTotal += added;
                total += added;
            }

            // --- Real RSS/Atom feeds ---
            foreach (var feed in crawler.RssFeeds.Where(f => !string.IsNullOrWhiteSpace(f)))
            {
                if (total >= maxNew || ++connectorCount > maxConnectors) break;
                var host = TryHost(feed);
                // Dalam Negeri mode: skip well-known foreign outlets (e.g. BBC) entirely.
                if (mode == CrawlerMode.DalamNegeri && IsForeignNewsHost(host)) continue;
                status.SetConnector($"RSS: {host}");
                total += await RunConnectorAsync($"RSS: {host}", SourceKind.News, trigger, mode,
                    () => FetchRssAsync(feed.Trim(), maxPerFeed, ct), ct, maxContent, perConnectorTimeout);
            }

            // --- Social / forum connectors (semua nonaktif secara default; nyalakan di Settings) ---
            foreach (var connector in connectors)
            {
                if (total >= maxNew || ++connectorCount > maxConnectors) break;
                if (!connector.IsEnabled(cfg)) continue;
                if (connector.IsConfigured(cfg))
                {
                    status.SetConnector(connector.Platform);
                    total += await RunConnectorAsync(connector.Platform, connector.Kind, trigger, mode,
                        () => connector.FetchAsync(cfg, ct), ct, maxContent, perConnectorTimeout);
                }
                else if (trigger == "Manual")
                {
                    // Surface "needs credentials" on manual runs, so operators see it in the log.
                    await LogRunAsync(connector.Platform, connector.Kind, trigger, DateTime.UtcNow,
                        TimeSpan.Zero, 0, 0, 0, false, "Kredensial belum diatur (isi di Pengaturan)");
                }
            }

            // --- Retensi data: buang post lama & trim log crawler agar DB tidak membengkak ---
            await PurgeAsync(crawler, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "RunOnceAsync gagal (trigger={Trigger})", trigger);
        }
        finally
        {
            status.EndPass(total);
        }
        return total;
    }

    // ---- Kata kunci pantauan aktif (untuk pencarian otomatis Google News) ----
    private async Task<List<string>> GetActiveKeywordsAsync(CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            return await db.WatchKeywords.Where(k => k.IsActive)
                .Select(k => k.Term).Distinct().ToListAsync(ct);
        }
        catch { return new List<string>(); }
    }

    // ---- Per-connector execution + logging + retry ----
    private async Task<int> RunConnectorAsync(string name, SourceKind kind, string trigger, CrawlerMode mode,
        Func<Task<IReadOnlyList<CollectedItem>>> fetch, CancellationToken ct,
        int maxContentLength, TimeSpan? perConnectorTimeout = null)
    {
        var started = DateTime.UtcNow;
        var sw = Stopwatch.StartNew();
        int found = 0, added = 0, dup = 0; var ok = true; string? err = null;
        var retryCount = Math.Clamp(_retryCount, 0, 5);
        try
        {
            // Per-connector timeout: cancel fetch jika terlalu lama
            using var cts = perConnectorTimeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            if (cts is not null)
                cts.CancelAfter(perConnectorTimeout.Value);
            var token = cts?.Token ?? ct;

            IReadOnlyList<CollectedItem> items = Array.Empty<CollectedItem>();
            var lastEx = (Exception?)null;
            for (var attempt = 0; attempt <= retryCount; attempt++)
            {
                try
                {
                    items = FilterByMode(await fetch(), mode);
                    break; // sukses
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastEx = ex;
                    if (attempt < retryCount)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)); // exponential backoff
                        logger.LogWarning(ex, "Connector {Name} attempt {Attempt}/{Max} gagal, retry dalam {Delay}s", name, attempt + 1, retryCount + 1, delay.TotalSeconds);
                        await Task.Delay(delay, token);
                    }
                }
            }
            if (items.Count == 0 && lastEx is not null) throw lastEx;
            found = items.Count;
            (added, dup) = await StoreItemsAsync(items, ct, maxContentLength);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { ok = false; err = ex.Message; logger.LogWarning(ex, "Connector {Name} failed", name); }
        sw.Stop();
        await LogRunAsync(name, kind, trigger, started, sw.Elapsed, found, added, dup, ok, err);
        return added;
    }

    // ---- Store collected items (dedup, sentiment, classify, geocode, persist, publish) ----
    // Batasi batch size untuk mencegah OOM jika ribuan item masuk sekaligus.
    private const int StoreBatchSize = 100;

    private async Task<(int Added, int Dup)> StoreItemsAsync(IReadOnlyList<CollectedItem> items, CancellationToken ct, int maxContentLength)
    {
        if (items.Count == 0) return (0, 0);
        // Batasi jumlah item yang diproses per connector
        if (items.Count > StoreBatchSize * 2)
        {
            logger.LogWarning("StoreItemsAsync: {Count} items terlalu banyak, dibatasi menjadi {Limit}", items.Count, StoreBatchSize * 2);
            items = items.Take(StoreBatchSize * 2).ToList();
        }
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
                    source = new Source
                    {
                        Name = it.SourceName, Kind = it.Kind, Url = it.Url,
                        Country = it.Language == "id" ? "Indonesia" : "Global",
                        TrustScore = 0.5
                    };
                    db.Sources.Add(source);
                }
                sourceCache[it.SourceName] = source;
            }

            var content = it.Content.Length > maxContentLength ? it.Content[..maxContentLength] : it.Content;
            var (score, label) = SentimentAnalyzer.Analyze(content);
            var catName = TopicClassifier.Classify(content);
            var category = catName is null ? null : categories.FirstOrDefault(c => c.Name == catName);
            if (catName is not null && category is null)
            {
                // The classifier knows categories that may be missing from the DB (e.g. Kesehatan,
                // Lingkungan) — create them on the fly so those posts are not silently dropped
                // from the category charts and the donut stays accurate.
                category = new Category { Name = catName, Color = CategoryColor(catName) };
                db.Categories.Add(category);
                categories.Add(category);
            }

            var post = new Post
            {
                Source = source,
                Category = category,
                Author = it.Author,
                AuthorHandle = it.AuthorHandle,
                Title = it.Title.Length > 250 ? it.Title[..250] : it.Title,
                Content = content,
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

    // ---- Retensi data: buang post lama + trim log crawler (aman, idempotent) ----
    private async Task PurgeAsync(CrawlerConfig crawler, CancellationToken ct)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Hapus post yang lebih tua dari RetentionDays (0 = nonaktif). PostMedia ikut terhapus
            // (cascade); Alert yang menunjuk ke post di-SetNull — tidak ada data lain yang rusak.
            if (crawler.RetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-crawler.RetentionDays);
                var old = await db.Posts.Where(p => p.PublishedAt < cutoff).ToListAsync(ct);
                if (old.Count > 0)
                {
                    db.Posts.RemoveRange(old);
                    await db.SaveChangesAsync(ct);
                    logger.LogInformation("Retensi: {Count} post lebih tua dari {Days} hari dihapus", old.Count, crawler.RetentionDays);
                }
            }

            // Batasi jumlah baris Log Aktivitas Crawler agar tabel tidak membengkak tak terbatas.
            var keep = Math.Max(10, crawler.MaxCrawlRunsToKeep);
            var excess = await db.CrawlRuns.OrderByDescending(r => r.StartedAt).Skip(keep).ToListAsync(ct);
            if (excess.Count > 0)
            {
                db.CrawlRuns.RemoveRange(excess);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Retensi: {Count} baris log crawler lama dibuang (maks {Keep} disimpan)", excess.Count, keep);
            }

            // Bersihkan sumber (Source) yang tidak lagi memiliki post apa pun — menjaga
            // agar tabel sumber tidak membengkak oleh entri yang sudah tidak relevan.
            // Hanya sumber yang dibuat secara dinamis oleh crawler (bukan seed DbSeeder)
            // yang punya jumlah post = 0 dihapus, untuk menghindari penghapusan data konfigurasi.
            var orphanSources = await db.Sources
                .Where(s => !db.Posts.Any(p => p.SourceId == s.Id))
                .ToListAsync(ct);
            if (orphanSources.Count > 0)
            {
                db.Sources.RemoveRange(orphanSources);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Retensi: {Count} sumber tanpa post dihapus", orphanSources.Count);
            }
        }
        catch { /* pembersihan tidak boleh menggagalkan siklus crawler */ }
    }

    // ---- RSS fetch → CollectedItems ----
    private async Task<IReadOnlyList<CollectedItem>> FetchRssAsync(string feedUrl, int maxItems, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("crawler");
        var resp = await http.GetAsync(feedUrl, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("RSS {Url} returned HTTP {Status}", feedUrl, (int)resp.StatusCode);
            resp.Dispose();
            return Array.Empty<CollectedItem>();
        }
        using var respClean = resp;
        await using var stream = await respClean.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        var feed = SyndicationFeed.Load(reader);
        if (feed is null) return Array.Empty<CollectedItem>();
        var host = TryHost(feedUrl);
        // Gunakan host sebagai nama sumber — stabil antar siklus dan konsisten dengan log
        // "RSS: <host>" serta daftar Sumber (dedup by name), bukan judul feed yang bisa berubah.
        var name = host;

        var items = new List<CollectedItem>();
        foreach (var item in feed.Items.Take(maxItems))
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
                Language: LanguageDetector.Detect(title, summary),
                Lat: geo?.Lat, Lon: geo?.Lon, Location: geo?.Name));
        }
        return items;
    }

    // ---- RSS fetch + keyword relevance filter ----
    private async Task<IReadOnlyList<CollectedItem>> FetchRssFilteredAsync(string feedUrl, int maxItems, string keyword, CancellationToken ct)
    {
        var all = await FetchRssAsync(feedUrl, maxItems * 3, ct); // ambil lebih banyak dulu, lalu filter
        var filtered = all.Where(it => IsKeywordRelevant(it.Title, it.Content, keyword)).ToList();
        return filtered.Take(maxItems).ToList();
    }

    // ---- Crawler mode (Dalam Negeri vs Luar Negeri) ----

    /// <summary>
    /// Dalam Negeri mode keeps only Indonesian-language items; Luar Negeri keeps everything.
    /// Applied centrally so scheduled runs and the manual "Crawl sekarang" button behave identically.
    /// </summary>
    private static IReadOnlyList<CollectedItem> FilterByMode(IReadOnlyList<CollectedItem> items, CrawlerMode mode)
    {
        if (mode != CrawlerMode.DalamNegeri) return items;
        return items.Where(i => i.Language == "id").ToList();
    }

    /// <summary>Well-known international news outlets skipped entirely in Dalam Negeri mode.</summary>
    private static readonly HashSet<string> ForeignNewsHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "feeds.bbci.co.uk", "bbc.co.uk", "bbc.com",
        "cnn.com", "edition.cnn.com", "nytimes.com", "theguardian.com", "guardian.co.uk",
        "reuters.com", "apnews.com", "ap.org", "aljazeera.com", "dw.com", "dw.de",
        "foxnews.com", "nbcnews.com", "abcnews.go.com", "cbsnews.com", "usatoday.com",
        "washingtonpost.com", "wsj.com", "ft.com", "economist.com", "bloomberg.com",
        "france24.com", "fr24.com", "rfi.fr", "channelnewsasia.com", "cna.asia",
        "scmp.com", "straitstimes.com", "thestar.com.my", "todayonline.com", "asiaone.com",
        "arabnews.com", "gulfnews.com", "khaleejtimes.com", "timesofindia.indiatimes.com",
        "japantimes.co.jp", "kyodonews.net", "globaltimes.cn", "hindustantimes.com",
        "smh.com.au", "abc.net.au", "stuff.co.nz", "nzherald.co.nz"
    };

    private static bool IsForeignNewsHost(string host)
        => ForeignNewsHosts.Contains(host)
           || ForeignNewsHosts.Any(h => host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));

    /// <summary>Stable colors for categories auto-created at collection time.</summary>
    private static string CategoryColor(string name) => name switch
    {
        "Kesehatan" => "#EC4899",
        "Lingkungan" => "#22C55E",
        _ => "#94A3B8"
    };

    private static string TryHost(string url) { try { return new Uri(url).Host; } catch { return url; } }
    private static string StripHtml(string html) =>
        System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ")).Trim();
}
