namespace CyberLens.Services.Collection;

/// <summary>
/// Scheduled data-collection worker. On each cycle it delegates to <see cref="CollectorService"/>
/// (real RSS feeds + real social connectors). The same collector powers the manual "Crawl sekarang"
/// button, so scheduled and on-demand collection share one code path.
/// Keeps <see cref="CrawlerStatusService"/> in sync so the UI can show whether it is running.
///
/// Circuit breaker: jika crawler gagal berturut-turut sebanyak MaxConsecutiveFailures kali,
/// crawler otomatis dimatikan untuk mencegah crash loop. Reset otomatis setelah 1 siklus berhasil.
/// </summary>
public class CrawlerService(
    IServiceProvider services,
    AppSettingsService settings,
    CrawlerStatusService status,
    ILogger<CrawlerService> logger) : BackgroundService
{
    private int _consecutiveFailures = 0;
    private bool _circuitOpen = false;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Delay awal agar app sempat start dan DB siap
        await Task.Delay(TimeSpan.FromSeconds(8), ct);
        while (!ct.IsCancellationRequested)
        {
            var cfg = settings.Current;
            status.Enabled = cfg.Crawler.Enabled;
            status.IntervalSeconds = Math.Max(15, cfg.Crawler.IntervalSeconds);
            try
            {
                if (cfg.Crawler.Enabled && !_circuitOpen)
                {
                    using var scope = services.CreateScope();
                    var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
                    var added = await collector.RunOnceAsync(cfg, "Scheduled", ct);
                    // Reset circuit breaker pada siklus sukses
                    _consecutiveFailures = 0;
                    if (_circuitOpen)
                    {
                        _circuitOpen = false;
                        logger.LogInformation("Circuit breaker reset — crawler pulih setelah siklus sukses (added={Added})", added);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                logger.LogWarning("Crawler cycle failed ({Count} consecutive): {Error}", _consecutiveFailures, ex.Message);

                // Circuit breaker: matikan crawler setelah N kegagalan berturut-turut
                var maxFailures = Math.Max(3, cfg.Crawler.MaxConsecutiveFailures);
                if (_consecutiveFailures >= maxFailures)
                {
                    _circuitOpen = true;
                    logger.LogError("CIRCUIT BREAKER: Crawler dimatikan setelah {Count} kegagalan berturut-turut. Crawler akan aktif kembali setelah reset manual atau restart app.", _consecutiveFailures);
                }
            }

            status.NextRunAt = DateTime.UtcNow.AddSeconds(status.IntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(status.IntervalSeconds), ct);
        }
    }
}
