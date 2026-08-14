using CyberLens.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services.Collection;

public record CrawlStats(int Runs, int FailedRuns, int ItemsAdded, int ItemsFound,
    double SuccessRate, int AvgDurationMs, int Connectors);
public record CrawlDaily(DateTime Date, int Added);
public record ConnectorStat(string Connector, string Kind, int Runs, int Added, int Failed);
public record CrawlLocation(string Location, int Count);

/// <summary>Queries over the crawler activity log (<see cref="CrawlRun"/>) for the Crawler Ops dashboard.</summary>
public class CrawlLogService(IDbContextFactory<CyberLensDbContext> dbFactory)
{
    public async Task<CrawlStats> GetStatsAsync(DateTime since)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var runs = await db.CrawlRuns.Where(r => r.StartedAt >= since)
            .Select(r => new { r.Success, r.ItemsAdded, r.ItemsFound, r.DurationMs, r.Connector }).ToListAsync();
        if (runs.Count == 0) return new CrawlStats(0, 0, 0, 0, 0, 0, 0);
        var failed = runs.Count(r => !r.Success);
        return new CrawlStats(
            runs.Count, failed,
            runs.Sum(r => r.ItemsAdded), runs.Sum(r => r.ItemsFound),
            Math.Round((runs.Count - failed) * 100.0 / runs.Count, 1),
            (int)runs.Average(r => r.DurationMs),
            runs.Select(r => r.Connector).Distinct().Count());
    }

    public async Task<List<CrawlRun>> GetRunsAsync(DateTime since, string? connector, string? status, string? trigger, int limit = 200)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.CrawlRuns.Where(r => r.StartedAt >= since);
        if (!string.IsNullOrWhiteSpace(connector)) q = q.Where(r => r.Connector == connector);
        if (status == "success") q = q.Where(r => r.Success);
        else if (status == "failed") q = q.Where(r => !r.Success);
        if (!string.IsNullOrWhiteSpace(trigger)) q = q.Where(r => r.Trigger == trigger);
        return await q.OrderByDescending(r => r.StartedAt).Take(limit).ToListAsync();
    }

    public async Task<List<CrawlDaily>> GetDailyAddedAsync(int days)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.Date.AddDays(-days + 1);
        var rows = await db.CrawlRuns.Where(r => r.StartedAt >= since)
            .Select(r => new { r.StartedAt, r.ItemsAdded }).ToListAsync();
        var byDay = rows.GroupBy(r => r.StartedAt.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.ItemsAdded));
        var list = new List<CrawlDaily>();
        for (var i = 0; i < days; i++)
        {
            var d = since.AddDays(i);
            list.Add(new CrawlDaily(d, byDay.GetValueOrDefault(d)));
        }
        return list;
    }

    public async Task<List<ConnectorStat>> GetPerConnectorAsync(DateTime since)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.CrawlRuns.Where(r => r.StartedAt >= since)
            .Select(r => new { r.Connector, r.Kind, r.ItemsAdded, r.Success }).ToListAsync();
        return rows.GroupBy(r => (r.Connector, r.Kind))
            .Select(g => new ConnectorStat(g.Key.Connector, g.Key.Kind.ToString(),
                g.Count(), g.Sum(x => x.ItemsAdded), g.Count(x => !x.Success)))
            .OrderByDescending(c => c.Added).ToList();
    }

    public async Task<List<string>> GetConnectorNamesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.CrawlRuns.Select(r => r.Connector).Distinct().OrderBy(c => c).ToListAsync();
    }

    /// <summary>Top locations among posts collected within the period (for the location filter/insight).</summary>
    public async Task<List<CrawlLocation>> GetTopLocationsAsync(DateTime since, int top = 12)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Posts.Where(p => p.CollectedAt >= since && p.LocationName != null)
            .Select(p => p.LocationName!).ToListAsync();
        return rows.GroupBy(l => l).Select(g => new CrawlLocation(g.Key, g.Count()))
            .OrderByDescending(x => x.Count).Take(top).ToList();
    }
}
