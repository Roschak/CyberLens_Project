using CyberLens.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services.Collection;

/// <summary>
/// Real-time keyword monitoring: scans newly collected posts against active watch keywords
/// and raises alerts on the NotificationBus (in-app toasts + bell).
/// </summary>
public class AlertMonitorService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    AppSettingsService settings,
    NotificationBus bus,
    ILogger<AlertMonitorService> logger) : BackgroundService
{
    private DateTime _lastScan = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(12), ct);
        while (!ct.IsCancellationRequested)
        {
            var cfg = settings.Current.Alerting;
            try
            {
                if (cfg.Enabled) await ScanAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Alert scan failed"); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, cfg.ScanIntervalSeconds)), ct);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var scanFrom = _lastScan;
        _lastScan = DateTime.UtcNow;

        var keywords = await db.WatchKeywords.Where(k => k.IsActive).ToListAsync(ct);
        if (keywords.Count == 0) return;

        var fresh = await db.Posts.Include(p => p.Source)
            .Where(p => p.CollectedAt >= scanFrom)
            .OrderBy(p => p.CollectedAt).Take(200).ToListAsync(ct);
        if (fresh.Count == 0) return;

        foreach (var post in fresh)
        {
            foreach (var kw in keywords)
            {
                var tagForm = kw.Term.Replace(' ', '-');
                if (!post.Content.Contains(kw.Term, StringComparison.OrdinalIgnoreCase) &&
                    !post.Tags.Contains(tagForm, StringComparison.OrdinalIgnoreCase)) continue;

                kw.HitCount++;
                var alert = new Alert
                {
                    KeywordId = kw.Id, PostId = post.Id, Severity = kw.Severity,
                    Title = $"Kata kunci \"{kw.Term}\" terdeteksi di {post.Source?.Name ?? "sumber"}",
                    Message = post.Content.Length > 180 ? post.Content[..177] + "..." : post.Content,
                };
                db.Alerts.Add(alert);
                await db.SaveChangesAsync(ct);
                if (kw.NotifyRealtime)
                {
                    alert.Keyword = kw;
                    alert.Post = post;
                    bus.PublishAlert(alert);
                }
            }
        }
    }
}
