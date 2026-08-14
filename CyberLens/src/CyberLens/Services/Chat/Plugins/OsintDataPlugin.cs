using System.ComponentModel;
using System.Text;
using CyberLens.Data;
using CyberLens.Services.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace CyberLens.Services.Chat.Plugins;

/// <summary>
/// The bridge between Bang Kevin and CyberLens's own data: query posts, sentiment, trends,
/// keywords, sources, alerts and the entity network so the assistant answers from real numbers.
/// </summary>
public class OsintDataPlugin(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    AnalyticsService analytics)
{
    [KernelFunction, Description("Get a high-level summary of the monitoring platform: total posts, posts today and last 7 days, active sources, unread alerts, average sentiment, top category, and dark web mentions.")]
    public async Task<string> GetOverview()
    {
        var s = await analytics.GetDashboardStatsAsync();
        return $"""
        Ringkasan CyberLens:
        - Total post terkumpul: {s.TotalPosts}
        - Post 24 jam terakhir: {s.PostsToday}
        - Post 7 hari terakhir: {s.Posts7d}
        - Sumber aktif: {s.ActiveSources}
        - Alert belum dibaca: {s.UnreadAlerts}
        - Kata kunci dipantau: {s.ActiveKeywords}
        - Rata-rata sentimen 7 hari (-1..1): {s.AvgSentiment7d}
        - Kategori teratas 7 hari: {s.TopCategory7d}
        - Mention dark web 7 hari: {s.DarkWebMentions7d}
        """;
    }

    [KernelFunction, Description("Get the sentiment breakdown (positive/neutral/negative counts) for the last N days.")]
    public async Task<string> GetSentiment([Description("Number of days to look back, default 7")] int days = 7)
    {
        var slices = await analytics.GetSentimentBreakdownAsync(days);
        var total = slices.Sum(s => s.Count);
        var sb = new StringBuilder($"Sentimen {days} hari terakhir (total {total} post):\n");
        foreach (var s in slices)
            sb.AppendLine($"- {s.Label}: {s.Count} ({(total == 0 ? 0 : s.Count * 100.0 / total):0.1}%)");
        return sb.ToString();
    }

    [KernelFunction, Description("Get the top trending topics/keywords ranked by growth over the recent window compared to the previous window.")]
    public async Task<string> GetTrendingTopics([Description("Window size in days, default 7")] int days = 7)
    {
        var topics = await analytics.GetTrendingTopicsAsync(days);
        if (topics.Count == 0) return "Belum ada topik yang cukup data untuk analisis tren.";
        var sb = new StringBuilder($"Topik naik daun ({days} hari):\n");
        foreach (var t in topics)
            sb.AppendLine($"- {t.Topic}: {t.Recent} mention (sebelumnya {t.Previous}, pertumbuhan {t.GrowthPct:+0.0;-0.0}%)");
        return sb.ToString();
    }

    [KernelFunction, Description("Search recent posts that match a keyword or phrase. Returns up to 'limit' latest matching posts with source, sentiment and snippet.")]
    public async Task<string> SearchPosts(
        [Description("Keyword or phrase to search in post content")] string keyword,
        [Description("Max results, default 8")] int limit = 8)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var posts = await db.Posts.Include(p => p.Source)
            .Where(p => p.Content.Contains(keyword))
            .OrderByDescending(p => p.PublishedAt)
            .Take(Math.Clamp(limit, 1, 20))
            .ToListAsync();
        if (posts.Count == 0) return $"Tidak ada post yang mengandung '{keyword}'.";
        var sb = new StringBuilder($"{posts.Count} post terbaru tentang '{keyword}':\n");
        foreach (var p in posts)
            sb.AppendLine($"- [{p.PublishedAt:yyyy-MM-dd HH:mm}] {p.Source?.Name} ({p.SentimentLabel}): {(p.Content.Length > 160 ? p.Content[..160] + "..." : p.Content)}");
        return sb.ToString();
    }

    [KernelFunction, Description("Get the count of posts per category for the last N days.")]
    public async Task<string> GetCategoryStats([Description("Days to look back, default 7")] int days = 7)
    {
        var cats = await analytics.GetCategoryBreakdownAsync(days);
        if (cats.Count == 0) return "Belum ada data kategori.";
        var sb = new StringBuilder($"Distribusi kategori {days} hari:\n");
        foreach (var c in cats) sb.AppendLine($"- {c.Name}: {c.Count}");
        return sb.ToString();
    }

    [KernelFunction, Description("List the currently active watch keywords and how many times each has been detected.")]
    public async Task<string> GetWatchKeywords()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var kws = await db.WatchKeywords.Where(k => k.IsActive).OrderByDescending(k => k.HitCount).ToListAsync();
        if (kws.Count == 0) return "Tidak ada kata kunci yang dipantau.";
        var sb = new StringBuilder("Kata kunci dipantau:\n");
        foreach (var k in kws) sb.AppendLine($"- {k.Term} (severity {k.Severity}, {k.HitCount} deteksi)");
        return sb.ToString();
    }

    [KernelFunction, Description("Get the most recent alerts triggered by watch keywords.")]
    public async Task<string> GetRecentAlerts([Description("Max alerts, default 8")] int limit = 8)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var alerts = await db.Alerts.Include(a => a.Keyword)
            .OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(limit, 1, 20)).ToListAsync();
        if (alerts.Count == 0) return "Tidak ada alert.";
        var sb = new StringBuilder("Alert terbaru:\n");
        foreach (var a in alerts)
            sb.AppendLine($"- [{a.CreatedAt:yyyy-MM-dd HH:mm}] ({a.Severity}) {a.Title}");
        return sb.ToString();
    }

    [KernelFunction, Description("List the top news/social sources being monitored with their type and trust score.")]
    public async Task<string> GetSources()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var sources = await db.Sources.Where(s => s.IsActive).OrderByDescending(s => s.TrustScore).ToListAsync();
        var sb = new StringBuilder("Sumber yang dipantau:\n");
        foreach (var s in sources)
            sb.AppendLine($"- {s.Name} ({s.Kind}, trust {s.TrustScore:0.00}, {s.Country})");
        return sb.ToString();
    }

    [KernelFunction, Description("Predict the volume of posts for the next N days using linear regression over recent daily volume. Optionally filter by a keyword.")]
    public async Task<string> PredictTrend(
        [Description("Days to forecast ahead, default 7")] int horizon = 7,
        [Description("Optional keyword to filter, empty for all posts")] string keyword = "")
    {
        var forecast = await analytics.PredictAsync(30, Math.Clamp(horizon, 1, 30),
            string.IsNullOrWhiteSpace(keyword) ? null : keyword);
        var future = forecast.Where(f => f.IsForecast).ToList();
        if (future.Count == 0) return "Data belum cukup untuk memprediksi.";
        var sb = new StringBuilder($"Prediksi volume {(string.IsNullOrWhiteSpace(keyword) ? "semua post" : $"'{keyword}'")} {horizon} hari ke depan:\n");
        foreach (var f in future) sb.AppendLine($"- {f.Date:yyyy-MM-dd}: ~{f.Value:0} post");
        return sb.ToString();
    }

    [KernelFunction, Description("Get the most mentioned entities (people, organizations, hashtags, locations, accounts) in the monitoring network.")]
    public async Task<string> GetTopEntities([Description("Max entities, default 10")] int limit = 10)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var nodes = await db.EntityNodes.OrderByDescending(n => n.Mentions)
            .Take(Math.Clamp(limit, 1, 30)).ToListAsync();
        var sb = new StringBuilder("Entitas paling banyak disebut:\n");
        foreach (var n in nodes) sb.AppendLine($"- {n.Name} ({n.Kind}): {n.Mentions} mention");
        return sb.ToString();
    }
}
