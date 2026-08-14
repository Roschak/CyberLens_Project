namespace CyberLens.Services.Collection;

/// <summary>
/// Scheduled data-collection worker. On each cycle it delegates to <see cref="CollectorService"/>
/// (real RSS + real social connectors + optional simulated stream). The same collector powers the
/// manual "Crawl sekarang" button, so scheduled and on-demand collection share one code path.
/// Keeps <see cref="CrawlerStatusService"/> in sync so the UI can show whether it is running.
/// </summary>
public class CrawlerService(
    IServiceProvider services,
    AppSettingsService settings,
    CrawlerStatusService status,
    ILogger<CrawlerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), ct);
        while (!ct.IsCancellationRequested)
        {
            var cfg = settings.Current;
            status.Enabled = cfg.Crawler.Enabled;
            status.IntervalSeconds = Math.Max(15, cfg.Crawler.IntervalSeconds);
            try
            {
                if (cfg.Crawler.Enabled)
                {
                    using var scope = services.CreateScope();
                    var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
                    await collector.RunOnceAsync(cfg, "Scheduled", ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Crawler cycle failed"); }

            status.NextRunAt = DateTime.UtcNow.AddSeconds(status.IntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(status.IntervalSeconds), ct);
        }
    }
}
