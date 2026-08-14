# REST API

External integration API under `/api/v1`, documented interactively at **`/swagger`**. Every request must include the API key in the `X-Api-Key` header (configured in Settings; default `cyberlens-demo-key`). The API can be disabled entirely in Settings.

Authentication failure returns `401`; a disabled API returns `503`.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/v1/stats` | Platform summary (posts today/7d/total, sources, unread alerts, avg sentiment, top category, dark-web mentions) |
| GET | `/api/v1/posts` | List/search posts. Query: `keyword`, `sentiment` (positive/neutral/negative), `sourceId`, `page`, `pageSize` (≤100) |
| GET | `/api/v1/posts/{id}` | Single post with media |
| GET | `/api/v1/sources` | Monitored sources with post counts |
| GET | `/api/v1/keywords` | Watch keywords |
| POST | `/api/v1/keywords` | Add a keyword — body `{ "term": "...", "severity": "Info|Warning|Critical" }` |
| GET | `/api/v1/alerts` | Alerts. Query: `unreadOnly`, `limit` |
| GET | `/api/v1/analytics/sentiment` | Sentiment breakdown. Query: `days` |
| GET | `/api/v1/analytics/trending` | Trending topics. Query: `days` |
| GET | `/api/v1/analytics/volume` | Daily post volume. Query: `days`, `keyword` |
| GET | `/api/v1/analytics/forecast` | AI volume forecast. Query: `horizon`, `keyword` |
| GET | `/api/v1/analytics/network` | Entity relationship graph (nodes + links) |
| GET | `/api/v1/analytics/geo` | Geospatial distribution. Query: `days` |
| POST | `/api/v1/reports` | Generate a report — body `{ "kind": "Daily|Weekly|Monthly", "format": "Pdf|Excel" }`; returns a download URL |

## Examples

```bash
KEY="cyberlens-demo-key"
BASE="http://localhost:5009"

# Summary
curl -H "X-Api-Key: $KEY" "$BASE/api/v1/stats"

# Search negative posts about a keyword
curl -H "X-Api-Key: $KEY" "$BASE/api/v1/posts?keyword=ransomware&sentiment=negative&pageSize=10"

# Add a watch keyword
curl -X POST -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
     -d '{"term":"kebocoran data","severity":"Critical"}' "$BASE/api/v1/keywords"

# 7-day volume forecast for a keyword
curl -H "X-Api-Key: $KEY" "$BASE/api/v1/analytics/forecast?horizon=7&keyword=banjir"

# Generate a weekly PDF report
curl -X POST -H "X-Api-Key: $KEY" -H "Content-Type: application/json" \
     -d '{"kind":"Weekly","format":"Pdf"}' "$BASE/api/v1/reports"
```

Report downloads are served from `/files/{path}` (returned as `downloadUrl`).
