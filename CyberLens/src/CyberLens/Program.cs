using CyberLens.Api;
using CyberLens.Components;
using CyberLens.Data;
using CyberLens.Services;
using CyberLens.Services.Analysis;
using CyberLens.Services.Chat;
using CyberLens.Services.Chat.Plugins;
using CyberLens.Services.Collection;
using CyberLens.Services.Collection.Social;
using CyberLens.Services.Reporting;
using CyberLens.Services.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// ---- Configuration (file-backed, editable in-app) ----
builder.Services.AddSingleton<AppSettingsService>();

// ---- Database (provider selected in config/cyberlens.settings.json) ----
builder.Services.AddDbContextFactory<CyberLensDbContext>((sp, options) =>
{
    var cfg = sp.GetRequiredService<AppSettingsService>().Current.Database;
    switch (cfg.Provider)
    {
        case "SqlServer": options.UseSqlServer(cfg.SqlServer); break;
        case "MySql": options.UseMySql(cfg.MySql, ServerVersion.AutoDetect(cfg.MySql)); break;
        case "PostgreSql": options.UseNpgsql(cfg.PostgreSql); break;
        default:
            var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
            Directory.CreateDirectory(dataDir);
            options.UseSqlite($"Data Source={Path.Combine(dataDir, "cyberlens.db")}");
            break;
    }
});

// ---- Core services ----
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("crawler", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient("web", c => c.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("ai", c => c.Timeout = TimeSpan.FromMinutes(5));

builder.Services.AddSingleton<StorageService>();
builder.Services.AddSingleton<NotificationBus>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<ReportService>();

// ---- Chatbot 'Bang Kevin' ----
builder.Services.AddScoped<UtilityPlugin>();
builder.Services.AddScoped<WebToolsPlugin>();
builder.Services.AddScoped<OsintDataPlugin>();
builder.Services.AddScoped<AiKernelFactory>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<AiAnalyticsService>();
builder.Services.AddSingleton<MarkdownService>();

// ---- Collection ----
builder.Services.AddSingleton<CrawlerStatusService>();
builder.Services.AddScoped<CollectorService>();
builder.Services.AddScoped<CrawlLogService>();
// Social/forum connectors (real APIs; keyed ones read credentials from settings)
builder.Services.AddSingleton<ISocialConnector, RedditConnector>();
builder.Services.AddSingleton<ISocialConnector, MastodonConnector>();
builder.Services.AddSingleton<ISocialConnector, YouTubeConnector>();
builder.Services.AddSingleton<ISocialConnector, TwitterConnector>();
builder.Services.AddSingleton<ISocialConnector, FacebookConnector>();
builder.Services.AddSingleton<ISocialConnector, ThreadsConnector>();
builder.Services.AddSingleton<ISocialConnector, TikTokConnector>();
builder.Services.AddSingleton<ISocialConnector, DarkWebConnector>();

// ---- Background workers ----
builder.Services.AddHostedService<CrawlerService>();
builder.Services.AddHostedService<AlertMonitorService>();
builder.Services.AddHostedService<ReportSchedulerService>();

// ---- Auth ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.AccessDeniedPath = "/login";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// ---- Blazor + API ----
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new()
{
    Title = "CyberLens OSINT API",
    Version = "v1",
    Description = "REST API untuk integrasi eksternal. Sertakan header X-Api-Key pada setiap request."
}));

var app = builder.Build();

// ---- Initialize database + seed sample data ----
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CyberLensDbContext>>();
    await using var db = await dbFactory.CreateDbContextAsync();
    await DbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "CyberLens API v1"));

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// ---- Auth endpoints (form posts from the login page) ----
app.MapPost("/auth/login", async (HttpContext http, AuthService auth) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var user = await auth.ValidateCredentialsAsync(username, password);
    if (user is null) return Results.Redirect("/login?error=1");
    await auth.SignInAsync(http, user);
    return Results.Redirect("/");
});
app.MapPost("/auth/logout", async (HttpContext http, AuthService auth) =>
{
    await auth.SignOutAsync(http);
    return Results.Redirect("/login");
});

// ---- File streaming from the active storage backend ----
app.MapGet("/files/{**path}", async (string path, StorageService storage) =>
{
    var result = await storage.OpenReadAsync(path);
    if (result is null) return Results.NotFound();
    return Results.Stream(result.Value.Stream, result.Value.ContentType);
});

// ---- REST API ----
app.MapCyberLensApi();

// Provide the base URL to ChatService so image attachments resolve to absolute URLs.
var serverUrl = builder.Configuration["urls"]?.Split(';').FirstOrDefault()
    ?? app.Urls.FirstOrDefault() ?? "http://localhost:5000";
ChatService.SetBaseUrl(serverUrl);

app.Run();
