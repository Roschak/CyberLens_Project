using CyberLens.Data;
using Microsoft.EntityFrameworkCore;

namespace CyberLens.Services.Reporting;

/// <summary>
/// Automatic report generation: daily, weekly (Monday) and monthly (1st) PDF reports,
/// created once per period when enabled in settings.
/// </summary>
public class ReportSchedulerService(
    IServiceProvider services,
    AppSettingsService settings,
    ILogger<ReportSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await RunOnceAsync(); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogWarning(ex, "Report scheduler cycle failed"); }
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }

    private async Task RunOnceAsync()
    {
        var cfg = settings.Current.Reporting;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CyberLensDbContext>>();
        var reports = scope.ServiceProvider.GetRequiredService<ReportService>();
        await using var ctx = await db.CreateDbContextAsync();

        var today = DateTime.UtcNow.Date;

        if (cfg.AutoDaily)
        {
            var start = today.AddDays(-1);
            if (!await ctx.Reports.AnyAsync(r => r.Kind == ReportKind.Daily && r.PeriodStart == start))
                await reports.GenerateAsync(ReportKind.Daily, ReportFormat.Pdf, start, today, null, "system");
        }
        if (cfg.AutoWeekly && today.DayOfWeek == DayOfWeek.Monday)
        {
            var start = today.AddDays(-7);
            if (!await ctx.Reports.AnyAsync(r => r.Kind == ReportKind.Weekly && r.PeriodStart == start))
                await reports.GenerateAsync(ReportKind.Weekly, ReportFormat.Pdf, start, today, null, "system");
        }
        if (cfg.AutoMonthly && today.Day == 1)
        {
            var start = today.AddMonths(-1);
            if (!await ctx.Reports.AnyAsync(r => r.Kind == ReportKind.Monthly && r.PeriodStart == start))
                await reports.GenerateAsync(ReportKind.Monthly, ReportFormat.Pdf, start, today, null, "system");
        }
    }
}
