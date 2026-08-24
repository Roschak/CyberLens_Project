# Configuration

All operational settings live in **`config/cyberlens.settings.json`** and are editable from the in-app **Settings** page (Admin only). `appsettings.json` holds only logging config. Secrets belong in the settings file, never in source.

> Database and Storage **provider** changes take effect after an app restart. AI, Tavily, crawler, alerting, API, and reporting changes apply immediately.

## Sections

### Database
| Key | Default | Notes |
|-----|---------|-------|
| `Provider` | `SQLite` | `SQLite` \| `SqlServer` \| `MySql` \| `PostgreSql` |
| `SQLite` | `Data Source=data/cyberlens.db` | |
| `SqlServer` / `MySql` / `PostgreSql` | — | connection string per provider |

### Storage
| Key | Default | Notes |
|-----|---------|-------|
| `Provider` | `FileSystem` | `FileSystem` \| `AzureBlob` \| `S3` \| `MinIO` |
| `FileSystemRoot` | `storage` | folder for uploads |
| `AzureBlobConnectionString`, `AzureBlobContainer` | — | Azure Blob |
| `S3AccessKey`, `S3SecretKey`, `S3Region`, `S3Bucket` | — | Amazon S3 |
| `MinioEndpoint`, `MinioAccessKey`, `MinioSecretKey`, `MinioBucket` | — | MinIO |

### Ai (Bang Kevin)
| Key | Default | Notes |
|-----|---------|-------|
| `Provider` | `OpenAI` | `OpenAI` \| `Anthropic` \| `Gemini` \| `Ollama` |
| `SystemPrompt` | (persona) | the assistant's persona |
| `Temperature` | `0.7` | 0–1 |
| `MaxTokens` | `2048` | |
| `OpenAIApiKey`, `OpenAIModel` | — / `gpt-4o-mini` | |
| `AnthropicApiKey`, `AnthropicModel` | — / `claude-sonnet-5` | |
| `GeminiApiKey`, `GeminiModel` | — / `gemini-2.0-flash` | |
| `OllamaEndpoint`, `OllamaModel` | `http://localhost:11434` / `llama3.1` | |

### Tavily
| Key | Notes |
|-----|-------|
| `ApiKey` | enables Bang Kevin's internet search function |

### Crawler
| Key | Default | Notes |
|-----|---------|-------|
| `Enabled` | `true` | master switch |
| `IntervalSeconds` | `120` | crawl cycle |
| `Mode` | `DalamNegeri` | `DalamNegeri` (Indonesia only) \| `LuarNegeri` (global) |
| `MaxItemsPerFeed` | `15` | max items taken from each feed/connector per cycle — bounds the volume of data scraped |
| `MaxNewPostsPerCycle` | `200` | hard cap on NEW posts stored per crawl cycle; once reached the remaining feeds are skipped until the next cycle (active keywords get priority) |
| `MaxContentLength` | `2000` | stored article text is truncated to this many characters — keeps the DB small and AI/token usage bounded |
| `RetentionDays` | `30` | posts older than N days are auto-deleted each cycle (`0` = keep forever) |
| `MaxCrawlRunsToKeep` | `500` | max rows kept in the Crawler Activity Log — the oldest entries are trimmed so the table never grows unbounded |
| `RssFeeds` | 20 Indonesian feeds | real RSS/Atom feed URLs to ingest — 8 nasional (Antara, Google News ID, CNN Indonesia, Tempo, CNBC Indonesia, Okezone, Detik, Sindonews) + 12 media daerah (Radar Bandung/Cirebon/Banten/Pekalongan/Makassar/Ambon, Jabar Ekspres, Berita Jatim, Harian Bhirawa, Waspada Medan, Riau Pos, Sumsel Update) |

> SQLite runs in **WAL mode** with a 30 s busy timeout so the crawler, alert monitor and report scheduler can write concurrently without `database is locked` errors.

### Social (social-media & forum connectors)
Per-platform sub-objects under `Social`. **All are OFF by default** — only the Indonesian RSS feeds and active watch-keyword searches run. Enable a platform manually when needed (Reddit/Mastodon need no credentials; the API-keyed ones need their key/token). See [crawler.md](crawler.md).

| Platform | Key fields | Credential |
|----------|-----------|------------|
| `Reddit` | `Enabled`, `Subreddits[]`, `MaxPerSubreddit` | none (public JSON) |
| `Mastodon` | `Enabled`, `Instance`, `Hashtags[]`, `MaxPerHashtag` | none (public timelines) |
| `YouTube` | `Enabled`, `ApiKey`, `SearchTerms[]`, `MaxResults` | YouTube Data API v3 key |
| `Twitter` | `Enabled`, `BearerToken`, `SearchTerms[]`, `MaxResults` | X API v2 Bearer token |
| `Facebook` | `Enabled`, `AccessToken`, `PageIds[]`, `MaxPerPage` | Graph API Page token |
| `Threads` | `Enabled`, `AccessToken`, `UserId`, `MaxResults` | Threads Graph token |
| `TikTok` | `Enabled`, `ClientKey`, `ClientSecret`, `SearchTerms[]` | approved TikTok app |

### DarkWeb (real dark-web monitoring)
Off by default. See [crawler.md](crawler.md) → Dark web monitoring.

| Key | Notes |
|-----|-------|
| `Enabled` | turn the real dark-web connector on |
| `TorProxy` | SOCKS5 proxy for Tor, e.g. `socks5://127.0.0.1:9050` (requires a running Tor client) |
| `OnionUrls[]` | `.onion` pages/feeds to scrape through Tor |
| `ThreatIntelApiUrl`, `ThreatIntelApiKey` | optional clearnet threat-intel JSON feed |
| `MaxPerSource` | items per source per pass |

### Alerting
| Key | Default | Notes |
|-----|---------|-------|
| `Enabled` | `true` | real-time keyword scanning |
| `ScanIntervalSeconds` | `20` | |

### Api
| Key | Default | Notes |
|-----|---------|-------|
| `Enabled` | `true` | REST API on/off |
| `ApiKey` | `cyberlens-demo-key` | sent by clients in `X-Api-Key` |

### Reporting
| Key | Default |
|-----|---------|
| `AutoDaily` / `AutoWeekly` / `AutoMonthly` | `true` |

## Going to production

1. Set a real database and storage provider.
2. Set a strong `Api.ApiKey`.
3. Set an AI provider key (or disable the chat).
4. Add more real `RssFeeds` (or integrate real social APIs) as needed.
5. Serve over HTTPS and change the demo user passwords.

> There is no simulated/demo data anymore: collection only stores items from real RSS/Atom feeds and real social APIs. A legacy cleanup runs automatically at startup to remove any demo rows left by older versions.
