using CyberLens.Data;
using CyberLens.Services;
using CyberLens.Services.Analysis;
using CyberLens.Services.Reporting;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Api;

/// <summary>
/// External integration REST API (/api/v1) secured by the X-Api-Key header.
/// Documented via Swagger/OpenAPI at /swagger.
/// </summary>
public static class ApiEndpoints
{
    public static void MapCyberLensApi(this WebApplication app)
    {
        var api = app.MapGroup("/api/v1")
            .AddEndpointFilter<ApiKeyFilter>()
            .WithTags("CyberLens API");

        api.MapGet("/stats", async (AnalyticsService analytics) =>
            Results.Ok(await analytics.GetDashboardStatsAsync()))
            .WithSummary("Platform summary statistics");

        api.MapGet("/posts", async (
            IDbContextFactory<CyberLensDbContext> dbf,
            string? keyword, string? sentiment, int? sourceId, int page = 1, int pageSize = 25) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var q = db.Posts.Include(p => p.Source).Include(p => p.Category).AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(p => p.Content.Contains(keyword));
            if (!string.IsNullOrWhiteSpace(sentiment)) q = q.Where(p => p.SentimentLabel == sentiment);
            if (sourceId is not null) q = q.Where(p => p.SourceId == sourceId);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var total = await q.CountAsync();
            var items = await q.OrderByDescending(p => p.PublishedAt)
                .Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.Title, p.Content, p.Author, p.AuthorHandle, p.Language, p.Url,
                    p.PublishedAt, p.SentimentScore, p.SentimentLabel, p.Likes, p.Shares, p.Comments,
                    p.LocationName, p.Latitude, p.Longitude, p.Tags,
                    Source = p.Source!.Name, SourceKind = p.Source.Kind.ToString(),
                    Category = p.Category != null ? p.Category.Name : null
                }).ToListAsync();
            return Results.Ok(new { total, page, pageSize, items });
        }).WithSummary("List/search collected posts");

        api.MapGet("/posts/{id:int}", async (int id, IDbContextFactory<CyberLensDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var post = await db.Posts.Include(p => p.Source).Include(p => p.Category).Include(p => p.Media)
                .FirstOrDefaultAsync(p => p.Id == id);
            return post is null ? Results.NotFound() : Results.Ok(post);
        }).WithSummary("Get a single post with media");

        api.MapGet("/sources", async (IDbContextFactory<CyberLensDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            return Results.Ok(await db.Sources.Select(s => new
            {
                s.Id, s.Name, Kind = s.Kind.ToString(), s.Url, s.Country, s.TrustScore, s.IsActive,
                PostCount = s.Posts.Count
            }).ToListAsync());
        }).WithSummary("List monitored sources");

        api.MapGet("/keywords", async (IDbContextFactory<CyberLensDbContext> dbf) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            return Results.Ok(await db.WatchKeywords.Select(k => new
            {
                k.Id, k.Term, Severity = k.Severity.ToString(), k.IsActive, k.HitCount
            }).ToListAsync());
        }).WithSummary("List watch keywords");

        api.MapPost("/keywords", async (KeywordDto dto, IDbContextFactory<CyberLensDbContext> dbf) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Term)) return Results.BadRequest("Term is required.");
            await using var db = await dbf.CreateDbContextAsync();
            var kw = new WatchKeyword
            {
                Term = dto.Term.Trim(),
                Severity = Enum.TryParse<AlertSeverity>(dto.Severity, true, out var s) ? s : AlertSeverity.Warning
            };
            db.WatchKeywords.Add(kw);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/keywords/{kw.Id}", new { kw.Id, kw.Term });
        }).WithSummary("Add a watch keyword");

        api.MapGet("/alerts", async (IDbContextFactory<CyberLensDbContext> dbf, bool? unreadOnly, int limit = 50) =>
        {
            await using var db = await dbf.CreateDbContextAsync();
            var q = db.Alerts.Include(a => a.Keyword).AsQueryable();
            if (unreadOnly == true) q = q.Where(a => !a.IsRead);
            var items = await q.OrderByDescending(a => a.CreatedAt).Take(Math.Clamp(limit, 1, 200))
                .Select(a => new
                {
                    a.Id, a.Title, a.Message, Severity = a.Severity.ToString(),
                    a.CreatedAt, a.IsRead, Keyword = a.Keyword != null ? a.Keyword.Term : null
                }).ToListAsync();
            return Results.Ok(items);
        }).WithSummary("List alerts");

        api.MapGet("/analytics/sentiment", async (AnalyticsService a, int days = 7) =>
            Results.Ok(await a.GetSentimentBreakdownAsync(days))).WithSummary("Sentiment breakdown");
        api.MapGet("/analytics/trending", async (AnalyticsService a, int days = 7) =>
            Results.Ok(await a.GetTrendingTopicsAsync(days))).WithSummary("Trending topics");
        api.MapGet("/analytics/volume", async (AnalyticsService a, int days = 30, string? keyword = null) =>
            Results.Ok(await a.GetDailyVolumeAsync(days, keyword))).WithSummary("Daily post volume");
        api.MapGet("/analytics/forecast", async (AnalyticsService a, int horizon = 7, string? keyword = null) =>
            Results.Ok(await a.PredictAsync(30, horizon, keyword))).WithSummary("Predicted volume (AI forecast)");
        api.MapGet("/analytics/network", async (AnalyticsService a) =>
            Results.Ok(await a.GetNetworkGraphAsync())).WithSummary("Entity relationship graph");
        api.MapGet("/analytics/geo", async (AnalyticsService a, int days = 30) =>
            Results.Ok(await a.GetGeoPointsAsync(days))).WithSummary("Geospatial distribution");

        api.MapPost("/reports", async (ReportRequestDto dto, ReportService reports) =>
        {
            var kind = Enum.TryParse<ReportKind>(dto.Kind, true, out var k) ? k : ReportKind.Daily;
            var format = Enum.TryParse<ReportFormat>(dto.Format, true, out var f) ? f : ReportFormat.Pdf;
            var end = DateTime.UtcNow;
            var start = kind switch
            {
                ReportKind.Weekly => end.AddDays(-7),
                ReportKind.Monthly => end.AddMonths(-1),
                _ => end.AddDays(-1)
            };
            var rec = await reports.GenerateAsync(kind, format, start, end, null, "api");
            return Results.Ok(new { rec.Id, rec.Title, DownloadUrl = $"/files/{rec.StoragePath}" });
        }).WithSummary("Generate a report (PDF/Excel)");
    }

    public record KeywordDto(string Term, string Severity);
    public record ReportRequestDto(string Kind, string Format);
}

/// <summary>Validates the X-Api-Key header against the configured API key.</summary>
public class ApiKeyFilter(AppSettingsService settings) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var cfg = settings.Current.Api;
        if (!cfg.Enabled) return Results.Problem("API dinonaktifkan.", statusCode: 503);
        var provided = ctx.HttpContext.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(provided) || provided != cfg.ApiKey)
            return Results.Unauthorized();
        return await next(ctx);
    }
}
