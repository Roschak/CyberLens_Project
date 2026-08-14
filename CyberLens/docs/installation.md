# Installation

## Prerequisites

- **.NET 10 SDK** (`dotnet --version` ≥ 10.0)
- A modern browser
- (Optional) A database server if not using SQLite; object storage if not using the local filesystem; an AI provider key for Bang Kevin.

## Run

```bash
cd src/CyberLens
dotnet run
```

The console prints the URL (e.g. `http://localhost:5009`). On first launch:

1. `config/cyberlens.settings.json` is created with defaults.
2. A SQLite database is created under `data/cyberlens.db`.
3. Sample data is seeded (~1,800 posts, 6 users, 16 sources, keywords, alerts, entity network).

Log in with `admin` / `admin`.

## Build for release

```bash
cd src/CyberLens
dotnet publish -c Release -o ./publish
./publish/CyberLens        # or CyberLens.exe on Windows
```

## Resetting the demo data

Stop the app, delete `data/cyberlens.db` (SQLite) and `config/cyberlens.settings.json`, then run again. For other databases, drop the database and restart.

## Switching database or storage

Open **Settings** (as an Admin) and change the provider and connection details, or edit `config/cyberlens.settings.json` directly. Database/storage provider changes require an app restart. See [database.md](database.md) and [storage.md](storage.md).

## Common issues

- **Bang Kevin returns a configuration error** — set a valid API key for the selected AI provider in Settings.
- **HTTPS redirect warning in the dev console** — harmless when running on HTTP only.
- **Charts blank** — they render after the interactive circuit connects; ensure the browser can reach the app's WebSocket and that `d3.v7.min.js` / `d3.layout.cloud` load (they come from a CDN; host them locally for offline use).
