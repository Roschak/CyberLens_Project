using ClosedXML.Excel;
using CyberLens.Data;
using CyberLens.Services.Analysis;
using CyberLens.Services.Storage;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CyberLens.Services.Reporting;

/// <summary>Generates PDF (QuestPDF) and Excel (ClosedXML) intelligence reports and stores them via IFileStorage.</summary>
public class ReportService(
    IDbContextFactory<CyberLensDbContext> dbFactory,
    StorageService storage,
    AuditService audit)
{
    public async Task<ReportRecord> GenerateAsync(ReportKind kind, ReportFormat format,
        DateTime periodStart, DateTime periodEnd, int? userId, string username)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var posts = await db.Posts.Include(p => p.Source).Include(p => p.Category)
            .Where(p => p.PublishedAt >= periodStart && p.PublishedAt < periodEnd)
            .OrderByDescending(p => p.PublishedAt).ToListAsync();
        var sentiment = posts.GroupBy(p => p.SentimentLabel).ToDictionary(g => g.Key, g => g.Count());
        var categories = posts.Where(p => p.Category != null)
            .GroupBy(p => p.Category!.Name).OrderByDescending(g => g.Count())
            .Select(g => (Name: g.Key, Count: g.Count())).ToList();
        var topKeywords = posts.SelectMany(p => p.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(t => t).OrderByDescending(g => g.Count()).Take(15)
            .Select(g => (Word: g.Key, Count: g.Count())).ToList();
        var alerts = await db.Alerts.Include(a => a.Keyword)
            .Where(a => a.CreatedAt >= periodStart && a.CreatedAt < periodEnd)
            .OrderByDescending(a => a.CreatedAt).ToListAsync();

        var title = $"Laporan {KindLabel(kind)} CyberLens {periodStart:yyyy-MM-dd} s/d {periodEnd:yyyy-MM-dd}";
        byte[] bytes = format == ReportFormat.Pdf
            ? BuildPdf(title, periodStart, periodEnd, posts, sentiment, categories, topKeywords, alerts)
            : BuildExcel(posts, sentiment, categories, topKeywords);

        var ext = format == ReportFormat.Pdf ? "pdf" : "xlsx";
        var path = $"reports/{DateTime.UtcNow:yyyy/MM}/cyberlens_{kind.ToString().ToLower()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{ext}";
        using var ms = new MemoryStream(bytes);
        var saved = await storage.SaveAsync(path, ms, StoragePath.GuessContentType($"x.{ext}"));

        var record = new ReportRecord
        {
            Title = title, Kind = kind, Format = format, StoragePath = saved,
            CreatedById = userId, PeriodStart = periodStart, PeriodEnd = periodEnd
        };
        db.Reports.Add(record);
        await db.SaveChangesAsync();
        await audit.LogAsync(username, "report.generate", $"{title} ({format})", userId);
        return record;
    }

    private static string KindLabel(ReportKind k) => k switch
    {
        ReportKind.Daily => "Harian", ReportKind.Weekly => "Mingguan",
        ReportKind.Monthly => "Bulanan", _ => "Kustom"
    };

    private static byte[] BuildPdf(string title, DateTime start, DateTime end, List<Post> posts,
        Dictionary<string, int> sentiment, List<(string Name, int Count)> categories,
        List<(string Word, int Count)> topKeywords, List<Alert> alerts)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().BorderBottom(2).PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("CYBERLENS // OSINT INTELLIGENCE REPORT").Bold().FontSize(9).FontColor("#FF4D00");
                        col.Item().Text(title).Bold().FontSize(16);
                    });
                    row.ConstantItem(140).AlignRight().Text($"Dibuat: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC").FontSize(8);
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    col.Item().Text("Ringkasan Eksekutif").Bold().FontSize(13);
                    col.Item().Text(
                        $"Selama periode {start:dd MMM yyyy} – {end:dd MMM yyyy} terkumpul {posts.Count} item dari " +
                        $"{posts.Select(p => p.SourceId).Distinct().Count()} sumber aktif. " +
                        $"Sentimen: {sentiment.GetValueOrDefault("positive")} positif, {sentiment.GetValueOrDefault("neutral")} netral, " +
                        $"{sentiment.GetValueOrDefault("negative")} negatif. Total {alerts.Count} alert terpicu.");

                    col.Item().Text("Distribusi Kategori").Bold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(2); });
                        table.Header(h =>
                        {
                            h.Cell().Background("#171410").Padding(4).Text("Kategori").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#171410").Padding(4).Text("Jumlah").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#171410").Padding(4).Text("Persentase").FontColor("#FFFFFF").Bold();
                        });
                        foreach (var (name, count) in categories)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(name);
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(count.ToString());
                            table.Cell().BorderBottom(0.5f).Padding(4)
                                .Text(posts.Count == 0 ? "0%" : $"{count * 100.0 / posts.Count:0.0}%");
                        }
                    });

                    col.Item().Text("Kata Kunci Teratas").Bold().FontSize(13);
                    col.Item().Text(string.Join("   ", topKeywords.Select(k => $"{k.Word} ({k.Count})")));

                    col.Item().Text($"Alert Terpicu ({alerts.Count})").Bold().FontSize(13);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(90); c.RelativeColumn(2); c.RelativeColumn(4); });
                        table.Header(h =>
                        {
                            h.Cell().Background("#171410").Padding(4).Text("Waktu").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#171410").Padding(4).Text("Kata Kunci").FontColor("#FFFFFF").Bold();
                            h.Cell().Background("#171410").Padding(4).Text("Ringkasan").FontColor("#FFFFFF").Bold();
                        });
                        foreach (var a in alerts.Take(30))
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(a.CreatedAt.ToString("dd/MM HH:mm"));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(a.Keyword?.Term ?? "-");
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(a.Message.Length > 120 ? a.Message[..117] + "..." : a.Message);
                        }
                    });

                    col.Item().Text("Item Terbaru").Bold().FontSize(13);
                    foreach (var p in posts.Take(15))
                        col.Item().BorderLeft(2).BorderColor("#FF4D00").PaddingLeft(8).Column(c2 =>
                        {
                            c2.Item().Text($"{p.PublishedAt:dd MMM HH:mm} — {p.Source?.Name} — {p.SentimentLabel}").FontSize(8).FontColor("#666666");
                            c2.Item().Text(p.Content.Length > 200 ? p.Content[..197] + "..." : p.Content);
                        });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("CyberLens OSINT Platform — Halaman ").FontSize(8);
                    t.CurrentPageNumber().FontSize(8);
                    t.Span(" dari ").FontSize(8);
                    t.TotalPages().FontSize(8);
                });
            });
        }).GeneratePdf();
    }

    private static byte[] BuildExcel(List<Post> posts, Dictionary<string, int> sentiment,
        List<(string Name, int Count)> categories, List<(string Word, int Count)> topKeywords)
    {
        using var wb = new XLWorkbook();

        var sum = wb.Worksheets.Add("Ringkasan");
        sum.Cell(1, 1).Value = "CyberLens — Ringkasan Laporan";
        sum.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sum.Cell(3, 1).Value = "Total item"; sum.Cell(3, 2).Value = posts.Count;
        sum.Cell(4, 1).Value = "Positif"; sum.Cell(4, 2).Value = sentiment.GetValueOrDefault("positive");
        sum.Cell(5, 1).Value = "Netral"; sum.Cell(5, 2).Value = sentiment.GetValueOrDefault("neutral");
        sum.Cell(6, 1).Value = "Negatif"; sum.Cell(6, 2).Value = sentiment.GetValueOrDefault("negative");
        var r = 8;
        sum.Cell(r++, 1).Value = "Kategori";
        foreach (var (name, count) in categories) { sum.Cell(r, 1).Value = name; sum.Cell(r++, 2).Value = count; }
        r++;
        sum.Cell(r++, 1).Value = "Kata kunci teratas";
        foreach (var (word, count) in topKeywords) { sum.Cell(r, 1).Value = word; sum.Cell(r++, 2).Value = count; }
        sum.Columns().AdjustToContents();

        var ws = wb.Worksheets.Add("Posts");
        string[] headers = { "Waktu", "Sumber", "Jenis", "Kategori", "Penulis", "Konten", "Sentimen", "Skor", "Likes", "Shares", "Lokasi", "URL" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#171410"))
                .Font.SetFontColor(XLColor.White);
        }
        var row = 2;
        foreach (var p in posts)
        {
            ws.Cell(row, 1).Value = p.PublishedAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(row, 2).Value = p.Source?.Name;
            ws.Cell(row, 3).Value = p.Source?.Kind.ToString();
            ws.Cell(row, 4).Value = p.Category?.Name;
            ws.Cell(row, 5).Value = p.Author;
            ws.Cell(row, 6).Value = p.Content.Length > 500 ? p.Content[..500] : p.Content;
            ws.Cell(row, 7).Value = p.SentimentLabel;
            ws.Cell(row, 8).Value = p.SentimentScore;
            ws.Cell(row, 9).Value = p.Likes;
            ws.Cell(row, 10).Value = p.Shares;
            ws.Cell(row, 11).Value = p.LocationName;
            ws.Cell(row, 12).Value = p.Url;
            row++;
        }
        ws.Columns(1, 5).AdjustToContents();
        ws.Column(6).Width = 80;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
