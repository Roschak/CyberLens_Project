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
| `IntervalSeconds` | `45` | crawl cycle |
| `SimulateSocialStreams` | `true` | demo mode; generates realistic simulated traffic |
| `RssFeeds` | `[]` | real RSS/Atom feed URLs to ingest |
| `DarkWebMonitoring` | `true` | include simulated dark-web sources |

### Social (social-media & forum connectors)
Per-platform sub-objects under `Social`. Reddit and Mastodon need no credentials (on by default); the rest are off until you add the platform's key/token and enable them. See [crawler.md](crawler.md).

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
4. Turn off `SimulateSocialStreams` and add real `RssFeeds` (or integrate real social APIs).
5. Serve over HTTPS and change the demo user passwords.
