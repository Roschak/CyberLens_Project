# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

CyberLens is an OSINT / media-monitoring platform: a single **Blazor Server (.NET 10)** app that hosts an interactive UI, a REST API, and background workers in one process. Built by **Gravicode Studios** (led by Kang Fadhil). Original requirements (Bahasa Indonesia) are in `requirements.md`; user-facing docs are in `README.md` / `README.id.md` and `docs/`.

## Commands

All commands run from `src/CyberLens/`.

```bash
dotnet build                 # compile
dotnet run                   # run (prints URL, e.g. http://localhost:5009)
dotnet publish -c Release -o ./publish
```

- **First run** auto-creates the SQLite DB (`data/cyberlens.db`) and seeds only configuration (users, categories, sources, watch keywords) via `Data/DbSeeder.cs` — no sample posts. A legacy cleanup in `DbSeeder` removes leftover demo rows (synthetic posts with `/item/` URLs, entity graph, simulator crawl logs, fake alerts) from older installs. Reset by deleting `data/cyberlens.db` and `config/cyberlens.settings.json`.
- **Log in** with `admin` / `admin` (all demo passwords equal the username).
- **No test project** exists. To verify changes, run the app and drive it: the login page is static SSR (form POST to `/auth/login`); pages need the interactive circuit. A headless-browser screenshot loop (puppeteer-core against installed Edge) is the way to verify charts/interactivity — see how the app was verified during initial build.
- On Windows, `dotnet build` fails with a file lock if the app is still running — stop it first: `powershell -Command "Stop-Process -Name CyberLens -Force"`.

## Architecture (the load-bearing parts)

- **Configuration is file-backed and in-app editable.** `Services/AppSettingsService.cs` (singleton) loads/saves `config/cyberlens.settings.json` (model: `Models/AppConfig.cs`). `appsettings.json` holds only logging. **Read `settings.Current` on each use** — never cache it — so the Settings page's edits apply live. DB/storage provider changes need a restart; everything else is immediate.
- **Database** is multi-provider (SQLite/SqlServer/MySql/PostgreSql), chosen at startup in `Program.cs` from settings. Always use `IDbContextFactory<CyberLensDbContext>` and a short-lived `await using` context (safe for Blazor Server + background threads). Analytics **filter in SQL, then aggregate in memory** — EF can't translate `GroupBy(...).Select(new Record(...))`, so materialize with `ToListAsync()` before grouping (see `AnalyticsService`).
- **Storage** is multi-backend behind `Services/Storage/IFileStorage.cs` (FileSystem/AzureBlob/S3/MinIO). `StorageService` is the singleton facade; files are always served via the `/files/{path}` endpoint.
- **Real-time** uses `Services/NotificationBus.cs` (in-process pub/sub). Background services (`Collection/CrawlerService`, `Collection/AlertMonitorService`) publish posts/alerts; `MainLayout` and pages subscribe for live toasts, the bell, and dashboard refresh. Remember to unsubscribe in `Dispose`.
- **AI (Bang Kevin)** — `Services/Chat/AiKernelFactory.cs` builds a Semantic Kernel per the selected provider. OpenAI/Gemini/Ollama use stock connectors; **Anthropic uses a custom `AnthropicChatCompletionService`** (raw Messages API + tool-use loop + image support). Kernel functions live in `Services/Chat/Plugins/` (UtilityPlugin, WebToolsPlugin, OsintDataPlugin). Default Anthropic model is `claude-sonnet-5` (configurable). Note: Fable 5 rejects `temperature`, so the Anthropic connector omits it for fable/mythos models. **AI Analytics** (`Services/Chat/AiAnalyticsService.cs`, page `/ai-analytics`) reuses the kernel to turn an analytics digest into a JSON intelligence brief (no tools) — provider-agnostic, degrades gracefully without a key.
- **GeoMap** uses **Leaflet** (CDN in `App.razor`); `clLeafletMap` in `charts.js` renders CartoDB light/dark tiles + sentiment circle markers. The 3D globe (`/globe`) is separate (Three.js).
- **Dark web**: `DarkWebConnector` (an `ISocialConnector`, `Kind=DarkWeb`) fetches `.onion` pages via a Tor SOCKS5 proxy (`SocketsHttpHandler` + `WebProxy("socks5://…")`) and/or a threat-intel JSON feed; config in `AppConfig.DarkWeb`, off by default.

## UI conventions

- **Rendering**: global Interactive Server. Draw D3 charts (`wwwroot/js/charts.js`) and the Three.js globe (`wwwroot/js/globe.js`) in `OnAfterRenderAsync`, never in `OnInitialized` — JS can't run during prerender. `globe.js` is a Three.js **ES module** imported by full CDN URL with custom drag/zoom (no `OrbitControls`) so it needs no import map — do **not** add a second `<importmap>`, Blazor already emits one via `<ImportMap>`.
- **Collection**: `CollectorService` (scoped) runs one pass over RSS + all enabled `ISocialConnector`s (`Services/Collection/Social/` — all **OFF by default**; enable in Settings) + **one Google News Indonesia search per active watch keyword** (`Keyword: <term>` connector), logging a `CrawlRun` per connector. There is **no simulated stream** — only real data is stored. `CrawlerService` (hosted) runs it scheduled; the Sources/Crawler pages "Crawl sekarang" button runs it manually (`trigger: "Manual"`). `CrawlerStatusService` (singleton) holds live running state for the strip indicator + `/crawler` dashboard; `CrawlLogService` queries the `CrawlRun` log. Adding a connector = implement `ISocialConnector` + register in Program.cs.
- **Design system** is entirely in `wwwroot/app.css` (neo-brutalism: hard offset shadows, CSS-variable theming with `data-theme`). Chart colors come from the validated dataviz reference palette baked into `charts.js` (light/dark arrays) — don't hand-pick chart hues.
- **Auth**: `Components/Pages/_Imports.razor` applies `[Authorize]` to all pages; `Login.razor`/`NotFound.razor` use `[AllowAnonymous]`; admin pages (`Users`, `Audit`, `Settings`) add `[Authorize(Roles = "Admin")]`.
- **Razor gotchas hit during build**: a `@inject` member name must not equal the component's own class name (e.g. page `Chat` can't inject a member named `Chat` → use `ChatSvc`). Wrap `new` expressions in Razor markup as `@(new ...)`. Each page owns its title via `<PageHeader>`; the layout topbar is action-only.

## Directory map

```
src/CyberLens/
├── Api/ApiEndpoints.cs        Minimal API /api/v1 + X-Api-Key filter
├── Components/
│   ├── Layout/                MainLayout (strip+bell+toasts), NavMenu, EmptyLayout
│   ├── Pages/                 15 pages + _Imports ([Authorize])
│   ├── Icon.razor             inline SVG icon set
│   └── PageHeader/StatTile/…  shared components
├── Data/                      Entities, CyberLensDbContext, DbSeeder, SampleContent
├── Models/AppConfig.cs        editable settings model
├── Services/
│   ├── Analysis/              SentimentAnalyzer, TopicClassifier, AnalyticsService
│   ├── Chat/                  AiKernelFactory, ChatService, Anthropic connector, plugins
│   ├── Collection/            CrawlerService, AlertMonitorService (BackgroundService)
│   ├── Reporting/             ReportService (QuestPDF/ClosedXML), scheduler
│   └── Storage/               IFileStorage + 4 backends + StorageService
└── wwwroot/                   app.css, js/charts.js, js/app.js
```

## Notes

- Collection is **real-data only**: RSS/Atom feeds (20 Indonesian feeds by default — 8 nasional + 12 media daerah) + Google News Indonesia keyword search + real social APIs (all social connectors OFF by default). No simulated stream, no sample posts.
- `<NoWarn>` in the csproj suppresses Semantic Kernel experimental warnings (`SKEXP*`).
