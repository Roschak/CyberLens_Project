using System.ComponentModel;
using System.Text;
using System.Text.Json;
using CyberLens.Services;
using Microsoft.SemanticKernel;

namespace CyberLens.Services.Chat.Plugins;

/// <summary>Kernel functions that reach the internet: Tavily search, page scraping, reading files from URLs.</summary>
public class WebToolsPlugin(IHttpClientFactory httpFactory, AppSettingsService settings)
{
    [KernelFunction, Description("Search the internet for up-to-date information using the Tavily search API. Returns top results with titles, URLs and snippets.")]
    public async Task<string> SearchInternet(
        [Description("The search query")] string query,
        [Description("Maximum number of results, default 5")] int maxResults = 5)
    {
        var apiKey = settings.Current.Tavily.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Tavily API key belum dikonfigurasi. Minta admin mengisinya di halaman Settings.";

        var http = httpFactory.CreateClient("web");
        var payload = new
        {
            api_key = apiKey,
            query,
            max_results = Math.Clamp(maxResults, 1, 10),
            search_depth = "basic",
            include_answer = true
        };
        try
        {
            using var resp = await http.PostAsJsonAsync("https://api.tavily.com/search", payload);
            if (!resp.IsSuccessStatusCode)
                return $"Pencarian gagal (HTTP {(int)resp.StatusCode}).";
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var sb = new StringBuilder();
            if (doc.RootElement.TryGetProperty("answer", out var ans) && ans.ValueKind == JsonValueKind.String)
                sb.AppendLine($"Ringkasan: {ans.GetString()}\n");
            if (doc.RootElement.TryGetProperty("results", out var results))
                foreach (var r in results.EnumerateArray())
                    sb.AppendLine($"- {r.GetProperty("title").GetString()}\n  {r.GetProperty("url").GetString()}\n  {r.GetProperty("content").GetString()}");
            return sb.Length > 0 ? sb.ToString() : "Tidak ada hasil.";
        }
        catch (Exception ex) { return $"Error saat mencari: {ex.Message}"; }
    }

    [KernelFunction, Description("Fetch and extract the readable text content of a web page URL.")]
    public async Task<string> ScrapePage([Description("The URL of the page to scrape")] string url)
    {
        var http = httpFactory.CreateClient("web");
        try
        {
            var html = await http.GetStringAsync(url);
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<script.*?</script>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<style.*?</style>", " ",
                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 6000 ? text[..6000] + "..." : text;
        }
        catch (Exception ex) { return $"Gagal mengambil halaman: {ex.Message}"; }
    }

    [KernelFunction, Description("Read the text content of a file located at a URL (txt, csv, json, markdown, etc.).")]
    public async Task<string> ReadFileFromUrl([Description("The URL of the file")] string url)
    {
        var http = httpFactory.CreateClient("web");
        try
        {
            var content = await http.GetStringAsync(url);
            return content.Length > 8000 ? content[..8000] + "..." : content;
        }
        catch (Exception ex) { return $"Gagal membaca file: {ex.Message}"; }
    }
}
