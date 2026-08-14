# Database providers

CyberLens uses EF Core 10 and supports four providers. Select one in **Settings → Database** (or in `config/cyberlens.settings.json`), set the matching connection string, and restart.

| Provider | `Provider` value | EF Core package |
|----------|------------------|-----------------|
| SQLite (default) | `SQLite` | `Microsoft.EntityFrameworkCore.Sqlite` |
| SQL Server | `SqlServer` | `Microsoft.EntityFrameworkCore.SqlServer` |
| MySQL / MariaDB | `MySql` | `Pomelo.EntityFrameworkCore.MySql` |
| PostgreSQL | `PostgreSql` | `Npgsql.EntityFrameworkCore.PostgreSQL` |

## Example connection strings

```
SQLite       Data Source=data/cyberlens.db
SQL Server   Server=localhost;Database=CyberLens;Trusted_Connection=True;TrustServerCertificate=True
MySQL        Server=localhost;Database=cyberlens;User=root;Password=secret;
PostgreSQL   Host=localhost;Database=cyberlens;Username=postgres;Password=secret
```

## Schema

The schema is created automatically with `EnsureCreated()` on first run, and seeded if empty. Entities: `AppUser`, `Source`, `Category`, `Post`, `PostMedia`, `WatchKeyword`, `Alert`, `EntityNode`, `EntityLink`, `ChatSession`, `ChatMessage`, `ChatAttachment`, `AuditLog`, `ReportRecord`. Indexes exist on `Post.PublishedAt`, `Post.Hash`, `Alert.CreatedAt`, `AuditLog.CreatedAt`, and unique `AppUser.Username`.

Analytics queries filter in SQL and aggregate small result sets in memory, so behavior is identical across all four providers.

> For production you may prefer EF Core Migrations over `EnsureCreated()`. Add a migration with `dotnet ef migrations add Initial` and switch the seeder to `db.Database.MigrateAsync()`.
