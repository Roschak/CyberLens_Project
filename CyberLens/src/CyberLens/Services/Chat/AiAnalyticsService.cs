using System.Text;
using System.Text.Json;
using CyberLens.Data;
using CyberLens.Services.Analysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace CyberLens.Services.Chat;

public record AiThreat(string Topic, string Reason);
public record AiInsight(bool Ok, string Model, string Summary, List<string> Findings,
    List<string> Recommendations, string RiskLevel, string RiskRationale,
    List<AiThreat> TopThreats, string Outlook, string? Error);

/// <summary>
/// Generates an AI intelligence brief from the crawled data: it digests the platform's own
/// analytics (volume, sentiment, trends, categories, keywords, geo, dark web) and asks the
/// configured LLM (via <see cref="AiKernelFactory"/>) for a structured summary, findings,
/// recommendations, risk assessment and outlook.
/// </summary>
public class AiAnalyticsService(
    AiKernelFactory kernelFactory,
    AnalyticsService analytics,
    IDbContextFactory<CyberLensDbContext> dbFactory)
{
    private static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    public async Task<string> BuildDigestAsync()
    {
        var s = await analytics.GetDashboardStatsAsync();
        var sent = await analytics.GetSentimentBreakdownAsync(7);
        var trend = await analytics.GetTrendingTopicsAsync(7);
        var cats = await analytics.GetCategoryBreakdownAsync(7);
        var words = await analytics.GetTopKeywordsAsync(7, 20);
        var geo = await analytics.GetGeoPointsAsync(14);

        await using var db = await dbFactory.CreateDbContextAsync();
        var since = DateTime.UtcNow.AddDays(-7);
        var topSources = await db.Posts.Where(p => p.PublishedAt >= since && p.Source != null)
            .GroupBy(p => p.Source!.Name).Select(g => new { g.Key, C = g.Count() })
            .OrderByDescending(x => x.C).Take(8).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("DATA OSINT CYBERLENS (7 hari terakhir kecuali disebutkan):");
        sb.AppendLine($"- Total post: {s.TotalPosts}, hari ini: {s.PostsToday}, 7 hari: {s.Posts7d}");
        sb.AppendLine($"- Sumber aktif: {s.ActiveSources}, alert belum dibaca: {s.UnreadAlerts}, kata kunci dipantau: {s.ActiveKeywords}");
        sb.AppendLine($"- Rata-rata sentimen (-1..1): {s.AvgSentiment7d}, kategori teratas: {s.TopCategory7d}, mention dark web: {s.DarkWebMentions7d}");
        sb.AppendLine($"- Sentimen: {string.Join(", ", sent.Select(x => $"{x.Label} {x.Count}"))}");
        sb.AppendLine($"- Kategori: {string.Join(", ", cats.Select(x => $"{x.Name} {x.Count}"))}");
        sb.AppendLine($"- Topik naik daun: {string.Join(", ", trend.Take(10).Select(t => $"{t.Topic} (+{t.GrowthPct}% , {t.Recent}x)"))}");
        sb.AppendLine($"- Kata kunci teratas: {string.Join(", ", words.Take(15).Select(w => $"{w.Word}:{w.Count}"))}");
        sb.AppendLine($"- Lokasi teratas: {string.Join(", ", geo.GroupBy(g => g.Location).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key}:{g.Count()}"))}");
        sb.AppendLine($"- Sumber teratas: {string.Join(", ", topSources.Select(x => $"{x.Key}:{x.C}"))}");
        return sb.ToString();
    }

    public async Task<AiInsight> GenerateAsync(CancellationToken ct = default)
    {
        var model = kernelFactory.ActiveModel;
        var digest = await BuildDigestAsync();
        try
        {
            var kernel = kernelFactory.Build();
            var chat = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(
                "Kamu adalah analis intelijen senior pada platform pemantauan media/OSINT CyberLens. " +
                "Analisis data yang diberikan dan hasilkan brief intelijen yang tajam, ringkas, dan actionable dalam Bahasa Indonesia. " +
                "Jawab HANYA dengan satu objek JSON valid (tanpa markdown, tanpa penjelasan tambahan) sesuai skema:\n" +
                "{\"summary\": string (2-3 kalimat ringkasan situasi), " +
                "\"findings\": string[] (5-7 temuan kunci), " +
                "\"recommendations\": string[] (4-6 rekomendasi konkret), " +
                "\"riskLevel\": \"Low\"|\"Medium\"|\"High\"|\"Critical\", " +
                "\"riskRationale\": string (alasan singkat level risiko), " +
                "\"topThreats\": [{\"topic\": string, \"reason\": string}] (2-4 ancaman utama), " +
                "\"outlook\": string (prediksi/prospek 7 hari ke depan)}");
            history.AddUserMessage(digest + "\n\nHasilkan brief intelijennya sekarang sebagai JSON.");

            var settings = new OpenAIPromptExecutionSettings { Temperature = 0.4, MaxTokens = 2000 };
            var result = await chat.GetChatMessageContentsAsync(history, settings, kernel, ct);
            var text = result.FirstOrDefault()?.Content ?? "";
            return Parse(text, model);
        }
        catch (Exception ex)
        {
            return new AiInsight(false, model, "", new(), new(), "Unknown", "", new(), "",
                $"Gagal memanggil model {model}: {ex.Message}. Periksa provider & API key di Pengaturan.");
        }
    }

    private static AiInsight Parse(string text, string model)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<InsightDto>(text[start..(end + 1)], J);
                if (dto is not null)
                    return new AiInsight(true, model, dto.Summary ?? "", dto.Findings ?? new(),
                        dto.Recommendations ?? new(), dto.RiskLevel ?? "Medium", dto.RiskRationale ?? "",
                        (dto.TopThreats ?? new()).Select(t => new AiThreat(t.Topic ?? "", t.Reason ?? "")).ToList(),
                        dto.Outlook ?? "", null);
            }
            catch { /* fall through to raw text */ }
        }
        // Model returned prose instead of JSON — surface it as the summary.
        return new AiInsight(true, model, text, new(), new(), "Medium", "", new(), "", null);
    }

    private class InsightDto
    {
        public string? Summary { get; set; }
        public List<string>? Findings { get; set; }
        public List<string>? Recommendations { get; set; }
        public string? RiskLevel { get; set; }
        public string? RiskRationale { get; set; }
        public List<ThreatDto>? TopThreats { get; set; }
        public string? Outlook { get; set; }
    }
    private class ThreatDto { public string? Topic { get; set; } public string? Reason { get; set; } }
}
