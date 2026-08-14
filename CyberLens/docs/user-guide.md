# User guide

## Logging in

Go to the app URL and sign in. Demo accounts (password = username): `admin`, `supervisor` (Admin), `analyst`, `analyst2` (Analyst), `viewer`, `viewer2` (Viewer).

## Navigation

The sidebar is grouped into **Intel**, **Operasi**, and **Admin**. The top classification strip shows live status and a WIB clock. The topbar has the notification bell (real-time alerts), a light/dark theme toggle, and sign-out.

## Pages

**Dashboard** — situational overview: key stats, volume-by-category trend, sentiment donut, trending topics, category distribution, and a keyword word cloud. Updates live as the crawler collects.

**AI Analytics** — an LLM-generated intelligence brief from the crawled data (summary, risk level, findings, recommendations, top threats, outlook) plus supporting charts. Auto-generates on open; regenerate with the button. Needs an AI provider key (Settings). See [ai-analytics.md](ai-analytics.md).

**Live Feed** — the raw collection stream. Filter by text, sentiment, or source type. New items arrive in real time.

**Tren & Prediksi** — volume with a 7-day AI forecast (dashed = predicted), trending topics with growth %, keyword cloud and frequency bars. Filter by keyword.

**Jaringan Entitas** — force-directed graph of people, organizations, hashtags, locations, and accounts. Drag nodes; size = mentions.

**Peta Geospasial** — a **Leaflet** map (real OpenStreetMap/CARTO tiles) with circle markers colored by sentiment and sized by volume, plus a hottest-locations list.

**Globe 3D Intelijen** — interactive Three.js Earth mapping OSINT spatially: sentiment heatmap, source markers, event-cluster bubbles, a threat layer, and a timeline you can play. Drag to rotate, scroll to zoom, toggle layers from the header. See [globe.md](globe.md).

**Dark Web** — simulated hidden-forum/marketplace findings (threat chatter, leaked-credential offers). Actor handles are partially redacted.

**Kata Kunci** (Analyst+) — add/enable/disable/remove watch keywords with a severity level. Detected keywords raise alerts.

**Crawler Ops** — collection statistics and the crawler activity log: stat cards, charts (items/day, success-fail, per-connector, top locations), and a filterable log table (period, connector, status, trigger). Shows the live crawler status and a **Crawl sekarang** button (Analyst/Admin). See [crawler.md](crawler.md).

**Alert** — all keyword hits, filterable (all / unread / critical). Mark individual or all as read.

**Sumber** — monitored sources with type, country, trust score, and post counts. Analysts/Admins get a **Crawl sekarang** button to run a live collection pass on demand (real RSS feeds + stream).

**Laporan** — generate PDF or Excel reports (daily/weekly/monthly) on demand; download from the archive. Automatic reports are scheduled in Settings.

**Bang Kevin** — the AI assistant, with clickable example prompts to get started. See [chatbot.md](chatbot.md).

**Pengguna / Audit Trail / Pengaturan** (Admin only) — manage users and roles, review the activity log, and edit all configuration.

## Roles

| Role | Can |
|------|-----|
| Viewer | view all intel & operations pages |
| Analyst | + manage keywords, generate reports |
| Admin | + manage users, view audit trail, edit settings |

## Theme

Use the sun/moon toggle in the topbar. Your choice is remembered in the browser.

---

Dibuat oleh **Gravicode Studios**, dipimpin oleh **Kang Fadhil**.
