namespace CyberLens.Models;

/// <summary>
/// Root of the editable application configuration. Persisted as JSON in config/cyberlens.settings.json
/// and fully editable from the Settings page. Never store secrets in appsettings.json.
/// </summary>
public class AppConfig
{
    public DatabaseConfig Database { get; set; } = new();
    public StorageConfig Storage { get; set; } = new();
    public AiConfig Ai { get; set; } = new();
    public TavilyConfig Tavily { get; set; } = new();
    public CrawlerConfig Crawler { get; set; } = new();
    public SocialConfig Social { get; set; } = new();
    public DarkWebConfig DarkWeb { get; set; } = new();
    public AlertingConfig Alerting { get; set; } = new();
    public ApiConfig Api { get; set; } = new();
    public ReportingConfig Reporting { get; set; } = new();
}

public class DatabaseConfig
{
    /// <summary>SQLite | SqlServer | MySql | PostgreSql</summary>
    public string Provider { get; set; } = "SQLite";
    public string SQLite { get; set; } = "Data Source=data/cyberlens.db";
    public string SqlServer { get; set; } = "Server=localhost;Database=CyberLens;Trusted_Connection=True;TrustServerCertificate=True";
    public string MySql { get; set; } = "Server=localhost;Database=cyberlens;User=root;Password=;";
    public string PostgreSql { get; set; } = "Host=localhost;Database=cyberlens;Username=postgres;Password=";
}

public class StorageConfig
{
    /// <summary>FileSystem | AzureBlob | S3 | MinIO</summary>
    public string Provider { get; set; } = "FileSystem";
    public string FileSystemRoot { get; set; } = "storage";
    public string AzureBlobConnectionString { get; set; } = "";
    public string AzureBlobContainer { get; set; } = "cyberlens";
    public string S3AccessKey { get; set; } = "";
    public string S3SecretKey { get; set; } = "";
    public string S3Region { get; set; } = "us-east-1";
    public string S3Bucket { get; set; } = "cyberlens";
    public string MinioEndpoint { get; set; } = "http://localhost:9000";
    public string MinioAccessKey { get; set; } = "";
    public string MinioSecretKey { get; set; } = "";
    public string MinioBucket { get; set; } = "cyberlens";
}

public class AiConfig
{
    /// <summary>OpenAI | Anthropic | Gemini | Ollama</summary>
    public string Provider { get; set; } = "OpenAI";
    public string SystemPrompt { get; set; } =
        "Kamu adalah 'Bang Kevin', asisten intelijen sumber terbuka (OSINT) untuk platform CyberLens. " +
        "Kamu ramah, ringkas, dan profesional; jawab dalam bahasa yang dipakai pengguna (Indonesia atau Inggris). " +
        "Gunakan function/tools yang tersedia untuk menjawab pertanyaan tentang data pemantauan media (posts, sentimen, tren, " +
        "kata kunci, sumber, alert, jaringan entitas) dan untuk mencari informasi di internet bila diperlukan. " +
        "Selalu sebutkan angka dan fakta dari hasil tools, jangan mengarang data. Format jawaban dengan Markdown yang rapi.";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2048;
    public string OpenAIApiKey { get; set; } = "";
    public string OpenAIModel { get; set; } = "gpt-4o-mini";
    public string AnthropicApiKey { get; set; } = "";
    public string AnthropicModel { get; set; } = "claude-sonnet-5";
    public string GeminiApiKey { get; set; } = "";
    public string GeminiModel { get; set; } = "gemini-2.0-flash";
    public string OllamaEndpoint { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1";
}

public class TavilyConfig
{
    public string ApiKey { get; set; } = "";
}

public class CrawlerConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 45;
    /// <summary>When true, generates realistic simulated social-media traffic (demo mode, no API keys needed).</summary>
    public bool SimulateSocialStreams { get; set; } = true;
    /// <summary>Real RSS/Atom feeds ingested live (news portals, Google News, etc.). Add or remove from Settings.</summary>
    public List<string> RssFeeds { get; set; } = new()
    {
        "https://www.antaranews.com/rss/terkini.xml",
        "https://news.google.com/rss?hl=id&gl=ID&ceid=ID:id",
        "https://feeds.bbci.co.uk/news/world/rss.xml"
    };
    public bool DarkWebMonitoring { get; set; } = true;
}

public class AlertingConfig
{
    public bool Enabled { get; set; } = true;
    public int ScanIntervalSeconds { get; set; } = 20;
}

/// <summary>
/// Real dark-web monitoring: fetches configured .onion pages through a Tor SOCKS5 proxy,
/// and/or pulls a threat-intelligence JSON feed (leaks/pastes) using an API key.
/// Disabled by default; requires a running Tor proxy and/or a threat-intel subscription.
/// </summary>
public class DarkWebConfig
{
    public bool Enabled { get; set; }
    /// <summary>SOCKS5 proxy for Tor, e.g. socks5://127.0.0.1:9050 (run a Tor client/daemon).</summary>
    public string TorProxy { get; set; } = "socks5://127.0.0.1:9050";
    /// <summary>.onion pages/feeds to scrape through Tor.</summary>
    public List<string> OnionUrls { get; set; } = new();
    /// <summary>Optional threat-intel JSON feed (leak/paste monitoring) reachable over clearnet.</summary>
    public string ThreatIntelApiUrl { get; set; } = "";
    public string ThreatIntelApiKey { get; set; } = "";
    public int MaxPerSource { get; set; } = 10;
}

/// <summary>
/// Social-media & forum connector settings. Reddit and Mastodon work with no credentials
/// (public APIs) and are on by default; YouTube/Twitter/Facebook/Threads/TikTok require the
/// platform's API key/token — set them here and enable.
/// </summary>
public class SocialConfig
{
    public RedditConfig Reddit { get; set; } = new();
    public MastodonConfig Mastodon { get; set; } = new();
    public YouTubeConfig YouTube { get; set; } = new();
    public TwitterConfig Twitter { get; set; } = new();
    public FacebookConfig Facebook { get; set; } = new();
    public ThreadsConfig Threads { get; set; } = new();
    public TikTokConfig TikTok { get; set; } = new();
}

public class RedditConfig
{
    public bool Enabled { get; set; } = true;                     // public JSON API, no auth
    public List<string> Subreddits { get; set; } = new() { "worldnews", "indonesia", "cybersecurity" };
    public int MaxPerSubreddit { get; set; } = 8;
}

public class MastodonConfig
{
    public bool Enabled { get; set; } = true;                     // public tag timelines, no auth
    public string Instance { get; set; } = "https://mastodon.social";
    public List<string> Hashtags { get; set; } = new() { "OSINT", "cybersecurity", "Indonesia" };
    public int MaxPerHashtag { get; set; } = 8;
}

public class YouTubeConfig
{
    public bool Enabled { get; set; }                             // requires API key
    public string ApiKey { get; set; } = "";
    public List<string> SearchTerms { get; set; } = new() { "keamanan siber", "OSINT" };
    public int MaxResults { get; set; } = 8;
}

public class TwitterConfig
{
    public bool Enabled { get; set; }                             // X API v2, requires Bearer token
    public string BearerToken { get; set; } = "";
    public List<string> SearchTerms { get; set; } = new() { "ransomware", "kebocoran data" };
    public int MaxResults { get; set; } = 10;
}

public class FacebookConfig
{
    public bool Enabled { get; set; }                             // Graph API, requires access token
    public string AccessToken { get; set; } = "";
    public List<string> PageIds { get; set; } = new();
    public int MaxPerPage { get; set; } = 10;
}

public class ThreadsConfig
{
    public bool Enabled { get; set; }                             // Threads Graph API, requires token
    public string AccessToken { get; set; } = "";
    public string UserId { get; set; } = "me";
    public int MaxResults { get; set; } = 10;
}

public class TikTokConfig
{
    public bool Enabled { get; set; }                             // TikTok API, requires approved credentials
    public string ClientKey { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public List<string> SearchTerms { get; set; } = new() { "berita" };
    public int MaxResults { get; set; } = 10;
}

public class ApiConfig
{
    public bool Enabled { get; set; } = true;
    /// <summary>Sent by external clients in the X-Api-Key header.</summary>
    public string ApiKey { get; set; } = "cyberlens-demo-key";
}

public class ReportingConfig
{
    public bool AutoDaily { get; set; } = true;
    public bool AutoWeekly { get; set; } = true;
    public bool AutoMonthly { get; set; } = true;
}
