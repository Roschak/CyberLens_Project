using CyberLens.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services.Analysis;

public record DashboardStats(int PostsToday, int Posts7d, int TotalPosts, int ActiveSources,
    int UnreadAlerts, int ActiveKeywords, double AvgSentiment7d, string TopCategory7d, int DarkWebMentions7d);
public record DailyPoint(DateTime Date, int Count);
public record SeriesDto(string Name, string Color, List<DailyPoint> Points);
public record SentimentSlice(string Label, int Count);
public record CategorySlice(string Name, string Color, int Count);
public record WordFreq(string Word, int Count);
public record TrendingTopic(string Topic, int Recent, int Previous, double GrowthPct);
public record GraphNode(int Id, string Name, string Kind, int Mentions);
public record GraphLink(int Source, int Target, double Weight, string Relation);
public record NetworkGraph(List<GraphNode> Nodes, List<GraphLink> Links);
public record GeoPoint(double Lat, double Lon, string Location, int Count, double AvgSentiment);
public record ForecastPoint(DateTime Date, double Value, bool IsForecast);
public record GlobePoint(double Lat, double Lon, string Location, double Sentiment, string SentimentLabel,
    string Category, string SourceKind, string SourceName, long TimeMs, int Intensity, bool Threat);
public record GlobeCluster(double Lat, double Lon, string Location, int Count, double AvgSentiment, string TopCategory);

/// <summary>
/// All aggregate analytics used by the dashboard, trends, chatbot tools and REST API.
/// Filters rows in SQL, aggregates small result sets in memory so every database provider behaves identically.
/// </summary>
public class AnalyticsService(IDbContextFactory<CyberLensDbContext> dbFactory)
{
    public async Task<DashboardStats> GetDashboardStatsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var d1 = now.AddDays(-1);
        var d7 = now.AddDays(-7);

        var postsToday = await db.Posts.CountAsync(p => p.PublishedAt >= d1);
        var posts7d = await db.Posts.CountAsync(p => p.PublishedAt >= d7);
        var total = await db.Posts.CountAsync();
        var sources = await db.Sources.CountAsync(s => s.IsActive);
        var unread = await db.Alerts.CountAsync(a => !a.IsRead);
        var kws = await db.WatchKeywords.CountAsync(k => k.IsActive);
        var avgSent = await db.Posts.Where(p => p.PublishedAt >= d7).Select(p => (double?)p.SentimentScore).AverageAsync() ?? 0;
        var topCat = await db.Posts.Where(p => p.PublishedAt >= d7 && p.Category != null)
            .GroupBy(p => p.Category!.Name).OrderByDescending(g => g.Count())
            .Select(g => g.Key).FirstOrDefaultAsync() ?? "-";
        var darkweb = await db.Posts.CountAsync(p => p.PublishedAt >= d7 && p.Source!.Kind == SourceKind.DarkWeb);

        return new DashboardStats(postsToday, posts7d, total, sources, unread, kws,
            Math.Round(avgSent, 3), topCat, darkweb);
    }

    /// <summary>Daily post counts per category for the last N days (top categories only).</summary>
    public async Task<List<SeriesDto>> GetCategorySeriesAsync(int days = 30, int topN = 5)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var rows = await db.Posts.Where(p => p.PublishedAt >= since && p.Category != null)
            .Select(p => new { p.PublishedAt, p.Category!.Name, p.Category.Color })
            .ToListAsync();

        return rows.GroupBy(r => (r.Name, r.Color))
            .OrderByDescending(g => g.Count()).Take(topN)
            .Select(g => new SeriesDto(g.Key.Name, g.Key.Color, FillDaily(
                g.GroupBy(x => x.PublishedAt.Date).ToDictionary(x => x.Key, x => x.Count()), since, days)))
            .ToList();
    }

    public async Task<List<DailyPoint>> GetDailyVolumeAsync(int days = 30, string? keyword = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var q = db.Posts.Where(p => p.PublishedAt >= since);
        if (!string.IsNullOrWhiteSpace(keyword))
            q = q.Where(p => p.Content.Contains(keyword) || p.Tags.Contains(keyword));
        var dates = await q.Select(p => p.PublishedAt).ToListAsync();
        return FillDaily(dates.GroupBy(d => d.Date).ToDictionary(g => g.Key, g => g.Count()), since, days);
    }

    public async Task<List<SentimentSlice>> GetSentimentBreakdownAsync(int days = 7)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.Posts.Where(p => p.PublishedAt >= since)
            .GroupBy(p => p.SentimentLabel)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        return new[] { "positive", "neutral", "negative" }
            .Select(l => new SentimentSlice(l, rows.FirstOrDefault(r => r.Key == l)?.Count ?? 0)).ToList();
    }

    public async Task<List<CategorySlice>> GetCategoryBreakdownAsync(int days = 7)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.Posts.Where(p => p.PublishedAt >= since && p.Category != null)
            .Select(p => new { p.Category!.Name, p.Category.Color }).ToListAsync();
        return rows.GroupBy(r => (r.Name, r.Color))
            .Select(g => new CategorySlice(g.Key.Name, g.Key.Color, g.Count()))
            .OrderByDescending(c => c.Count).ToList();
    }

    public async Task<List<WordFreq>> GetTopKeywordsAsync(int days = 7, int topN = 40)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var tags = await db.Posts.Where(p => p.PublishedAt >= since && p.Tags != "")
            .Select(p => p.Tags).ToListAsync();
        return tags.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(t => t.Trim())
            .Select(g => new WordFreq(g.Key, g.Count()))
            .OrderByDescending(w => w.Count).Take(topN).ToList();
    }

    /// <summary>Topics rising fastest: recent window vs the window before it.</summary>
    public async Task<List<TrendingTopic>> GetTrendingTopicsAsync(int windowDays = 7, int topN = 10)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var recentStart = now.AddDays(-windowDays);
        var prevStart = now.AddDays(-windowDays * 2);

        var rows = await db.Posts.Where(p => p.PublishedAt >= prevStart && p.Tags != "")
            .Select(p => new { p.PublishedAt, p.Tags }).ToListAsync();

        var recent = CountTags(rows.Where(r => r.PublishedAt >= recentStart).Select(r => r.Tags));
        var previous = CountTags(rows.Where(r => r.PublishedAt < recentStart).Select(r => r.Tags));

        return recent.Select(kv =>
            {
                previous.TryGetValue(kv.Key, out var prev);
                var growth = prev == 0 ? kv.Value * 100.0 : (kv.Value - prev) * 100.0 / prev;
                return new TrendingTopic(kv.Key, kv.Value, prev, Math.Round(growth, 1));
            })
            .Where(t => t.Recent >= 3)
            .OrderByDescending(t => t.GrowthPct).ThenByDescending(t => t.Recent)
            .Take(topN).ToList();
    }

    public async Task<NetworkGraph> GetNetworkGraphAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var nodes = await db.EntityNodes
            .Select(n => new GraphNode(n.Id, n.Name, n.Kind.ToString(), n.Mentions)).ToListAsync();
        var links = await db.EntityLinks
            .Select(l => new GraphLink(l.SourceNodeId, l.TargetNodeId, l.Weight, l.RelationType)).ToListAsync();
        return new NetworkGraph(nodes, links);
    }

    public async Task<List<GeoPoint>> GetGeoPointsAsync(int days = 30)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.Posts
            .Where(p => p.PublishedAt >= since && p.Latitude != null && p.Longitude != null)
            .Select(p => new { p.LocationName, p.Latitude, p.Longitude, p.SentimentScore }).ToListAsync();
        return rows.GroupBy(r => r.LocationName ?? "?")
            .Select(g => new GeoPoint(
                Math.Round(g.Average(x => x.Latitude!.Value), 4),
                Math.Round(g.Average(x => x.Longitude!.Value), 4),
                g.Key, g.Count(), Math.Round(g.Average(x => x.SentimentScore), 3)))
            .OrderByDescending(p => p.Count).ToList();
    }

    /// <summary>
    /// Geo-located posts for the 3D globe: one point per post that has coordinates, carrying
    /// sentiment, source kind, category, timestamp (for the timeline), intensity and a threat flag
    /// (dark-web source or security category). Capped to the most recent <paramref name="max"/> points.
    /// </summary>
    public async Task<List<GlobePoint>> GetGlobePointsAsync(int days = 30, int max = 700)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.Posts
            .Where(p => p.PublishedAt >= since && p.Latitude != null && p.Longitude != null)
            .OrderByDescending(p => p.PublishedAt)
            .Take(max)
            .Select(p => new
            {
                p.Latitude, p.Longitude, p.LocationName, p.SentimentScore, p.SentimentLabel,
                Category = p.Category != null ? p.Category.Name : "Lainnya",
                Kind = p.Source!.Kind, SourceName = p.Source.Name,
                p.PublishedAt, p.Likes, p.Shares
            })
            .ToListAsync();

        return rows.Select(r => new GlobePoint(
            Math.Round(r.Latitude!.Value, 4), Math.Round(r.Longitude!.Value, 4),
            r.LocationName ?? "?", Math.Round(r.SentimentScore, 3), r.SentimentLabel,
            r.Category, r.Kind.ToString(), r.SourceName,
            new DateTimeOffset(DateTime.SpecifyKind(r.PublishedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            Math.Clamp(1 + (r.Likes + r.Shares) / 400, 1, 12),
            r.Kind == SourceKind.DarkWeb || r.Category == "Keamanan"))
            .ToList();
    }

    /// <summary>Event clustering: geo points grouped by location, sized by count, for the globe bubble layer.</summary>
    public async Task<List<GlobeCluster>> GetGlobeClustersAsync(int days = 30)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-days);
        var rows = await db.Posts
            .Where(p => p.PublishedAt >= since && p.Latitude != null && p.Longitude != null)
            .Select(p => new { p.Latitude, p.Longitude, p.LocationName, p.SentimentScore,
                Category = p.Category != null ? p.Category.Name : "Lainnya" })
            .ToListAsync();
        return rows.GroupBy(r => r.LocationName ?? "?")
            .Select(g => new GlobeCluster(
                Math.Round(g.Average(x => x.Latitude!.Value), 4),
                Math.Round(g.Average(x => x.Longitude!.Value), 4),
                g.Key, g.Count(), Math.Round(g.Average(x => x.SentimentScore), 3),
                g.GroupBy(x => x.Category).OrderByDescending(x => x.Count()).First().Key))
            .OrderByDescending(c => c.Count).ToList();
    }

    /// <summary>
    /// AI-based issue prediction: least-squares linear regression over daily volume,
    /// projected <paramref name="horizon"/> days ahead.
    /// </summary>
    public async Task<List<ForecastPoint>> PredictAsync(int historyDays = 30, int horizon = 7, string? keyword = null)
    {
        var history = await GetDailyVolumeAsync(historyDays, keyword);
        var pts = history.Select((p, i) => (X: (double)i, Y: (double)p.Count)).ToList();
        var result = history.Select(h => new ForecastPoint(h.Date, h.Count, false)).ToList();
        if (pts.Count < 5) return result;

        var n = pts.Count;
        var sumX = pts.Sum(p => p.X);
        var sumY = pts.Sum(p => p.Y);
        var sumXY = pts.Sum(p => p.X * p.Y);
        var sumX2 = pts.Sum(p => p.X * p.X);
        var denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-9) return result;
        var slope = (n * sumXY - sumX * sumY) / denom;
        var intercept = (sumY - slope * sumX) / n;

        var lastDate = history[^1].Date;
        for (var i = 1; i <= horizon; i++)
        {
            var x = n - 1 + i;
            result.Add(new ForecastPoint(lastDate.AddDays(i), Math.Max(0, Math.Round(slope * x + intercept, 1)), true));
        }
        return result;
    }

    private static List<DailyPoint> FillDaily(Dictionary<DateTime, int> counts, DateTime since, int days)
    {
        var list = new List<DailyPoint>(days);
        for (var i = 0; i < days; i++)
        {
            var d = since.AddDays(i);
            list.Add(new DailyPoint(d, counts.GetValueOrDefault(d)));
        }
        return list;
    }

    private static Dictionary<string, int> CountTags(IEnumerable<string> tagStrings)
        => tagStrings.SelectMany(t => t.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(t => t.Trim()).ToDictionary(g => g.Key, g => g.Count());
}
