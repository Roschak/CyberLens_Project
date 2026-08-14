# Security

## Authentication

- Cookie authentication (7-day sliding expiration).
- Passwords are hashed with **PBKDF2-SHA256**, 100,000 iterations, a per-user 16-byte salt, verified in constant time.
- The login form posts to `/auth/login`; the cookie is issued only on a valid hash match.

## Authorization

- Every page requires authentication (folder-level `[Authorize]`); Login and the 404 page opt out.
- Admin pages (Users, Audit Trail, Settings) require the **Admin** role.
- Roles: Viewer, Analyst, Admin.

## REST API

- Gated by the `X-Api-Key` header, checked by an endpoint filter on the whole `/api/v1` group.
- The key is configurable in Settings; change it from the default before exposing the API.
- The API can be fully disabled in Settings.

## Audit trail

User actions (login/logout, keyword changes, report generation, user creation, settings updates) are written to an append-only `AuditLog` with username, action, detail, IP, and timestamp. Reviewable by admins on the Audit Trail page.

## Storage & data handling

- Storage paths are sanitized (`..` rejected) on every backend before file access.
- Secrets (API keys, connection strings) are stored in `config/cyberlens.settings.json`, not in source. Keep this file out of version control and restrict its permissions.
- Dark-web actor identifiers are partially redacted in the UI.

## Hardening checklist for production

1. Serve over **HTTPS**; keep `UseHsts` enabled (already on outside Development).
2. Change **all demo passwords** and remove unused demo accounts.
3. Set a strong **`Api.ApiKey`**.
4. Move `config/cyberlens.settings.json` to a secret store or lock down file permissions.
5. Use a production database with least-privilege credentials; consider EF Core Migrations instead of `EnsureCreated()`.
6. Restrict outbound network access for the crawler/scraper if ingesting untrusted URLs.
7. Put the app behind a reverse proxy with rate limiting for the public REST API.

---

Dibuat oleh **Gravicode Studios**, dipimpin oleh **Kang Fadhil**.
