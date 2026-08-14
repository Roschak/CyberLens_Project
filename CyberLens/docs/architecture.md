# Architecture

CyberLens is a single ASP.NET Core (.NET 10) application that hosts a Blazor Server UI, a REST API, and three background workers in one process.

```
┌──────────────────────────────────────────────────────────────────┐
│                        CyberLens (one host)                        │
│                                                                    │
│  Blazor Server UI ──┐                                              │
│  REST API (/api/v1) ─┼─→ Services ─→ EF Core ─→ DB provider        │
│  /files/{path} ──────┘        │                (SQLite/SQLServer/  │
│                               │                 MySQL/PostgreSQL)  │
│  Background workers:          ├─→ IFileStorage ─→ FS/Azure/S3/MinIO│
│   • CrawlerService            │                                    │
│   • AlertMonitorService       └─→ Semantic Kernel ─→ OpenAI/       │
│   • ReportSchedulerService                          Anthropic/     │
│                                                     Gemini/Ollama  │
│  NotificationBus (in-process pub/sub) ──→ live UI updates          │
└──────────────────────────────────────────────────────────────────┘
```

## Layers

**Configuration** — `AppSettingsService` (singleton) loads and persists `config/cyberlens.settings.json`. Every consumer reads `settings.Current` on each use, so in-app edits apply without recompiling. A `Changed` event lets the storage layer rebuild its backend when the provider changes.

**Data** — EF Core with `IDbContextFactory<CyberLensDbContext>` (each operation gets a fresh short-lived context, which is safe for Blazor Server and background threads). The provider is chosen at startup from settings. `DbSeeder` creates the schema and seeds ~1,800 realistic posts, users, sources, keywords, alerts, an entity network, and a sample chat on first run.

**Services**
- `Analysis/` — `SentimentAnalyzer` (ID+EN lexicon, negation), `TopicClassifier` (keyword rules), `AnalyticsService` (all dashboard/trend/network/geo aggregates + linear-regression forecast). Aggregates filter rows in SQL and group small result sets in memory so every DB provider behaves identically.
- `Collection/` — `CollectorService` performs one collection pass (real RSS/Atom ingest + optional simulated stream) and is shared by two callers: `CrawlerService` (a `BackgroundService` running it on a schedule) and the manual **Crawl sekarang** button on the Sources page. `AlertMonitorService` (a `BackgroundService`) scans new posts against watch keywords.
- `Reporting/` — `ReportService` (QuestPDF + ClosedXML) and `ReportSchedulerService` (auto daily/weekly/monthly).
- `Storage/` — `IFileStorage` with `FileSystemStorage`, `AzureBlobStorage`, `S3Storage` (also serves MinIO), fronted by `StorageService`.
- `Chat/` — `AiKernelFactory` builds a Semantic Kernel for the selected provider, loads plugins, and (for Anthropic) uses a custom `AnthropicChatCompletionService` with a tool-use loop. `ChatService` manages sessions and orchestrates prompts. `MarkdownService` renders replies with Markdig.

**Real-time** — `NotificationBus` is an in-process pub/sub. Background workers publish new posts and alerts; the layout and pages subscribe and update connected Blazor circuits (notification bell, toasts, live feed counters, dashboard refresh).

## Request rendering

The app uses global **Interactive Server** rendering. Pages are prerendered on the server, then a SignalR circuit makes them interactive. D3 charts (`wwwroot/js/charts.js`) and the Three.js globe (`wwwroot/js/globe.js`) are drawn in `OnAfterRenderAsync` via JS interop, so they run only after the circuit is live, never during prerender. `globe.js` is a Three.js ES module imported by full CDN URL (no import map, so no conflict with Blazor's `<ImportMap>`); it implements its own camera drag/zoom instead of `OrbitControls`.

## Authentication & authorization

Cookie authentication. The login form posts to a `/auth/login` minimal endpoint that validates the PBKDF2 hash and issues the cookie. A folder-level `[Authorize]` on `Components/Pages` protects every page (Login and NotFound opt out with `[AllowAnonymous]`); admin pages add `[Authorize(Roles = "Admin")]`. The REST API is separately gated by the `X-Api-Key` header via an endpoint filter.
