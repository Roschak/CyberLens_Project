using Microsoft.EntityFrameworkCore;
using CyberLens.Services;

namespace CyberLens.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(CyberLensDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // SQLite: aktifkan WAL agar reader dan writer bisa berjalan bersamaan tanpa konflik
        // "database is locked" (crawler menulis sementara halaman dibaca). Aman diabaikan
        // untuk provider non-SQLite.
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            try { await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;"); }
            catch { /* non-fatal: DB tetap berfungsi dalam mode journal default */ }
        }

        // Seed Users if empty
        if (!await db.Users.AnyAsync())
        {
            var adminUser = new AppUser
            {
                Username = "admin",
                PasswordHash = PasswordHasher.Hash("admin"),
                DisplayName = "Kang Fadhil (Admin)",
                Role = UserRole.Admin,
                Email = "admin@cyberlens.id",
                AvatarColor = "#FF4D00",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                LastLoginAt = DateTime.UtcNow.AddHours(-1)
            };

            var analystUser = new AppUser
            {
                Username = "analyst1",
                PasswordHash = PasswordHasher.Hash("analyst1"),
                DisplayName = "Budi Santoso (Senior Analyst)",
                Role = UserRole.Analyst,
                Email = "budi@cyberlens.id",
                AvatarColor = "#3B82F6",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-45),
                LastLoginAt = DateTime.UtcNow.AddHours(-3)
            };

            var operatorUser = new AppUser
            {
                Username = "operator1",
                PasswordHash = PasswordHasher.Hash("operator1"),
                DisplayName = "Siti Rahma (OSINT Operator)",
                Role = UserRole.Operator,
                Email = "siti@cyberlens.id",
                AvatarColor = "#10B981",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                LastLoginAt = DateTime.UtcNow.AddHours(-5)
            };

            var kevinAiUser = new AppUser
            {
                Username = "kevin_ai",
                PasswordHash = PasswordHasher.Hash("kevin_ai"),
                DisplayName = "Bang Kevin (AI Assistant)",
                Role = UserRole.Analyst,
                Email = "bangkevin@cyberlens.id",
                AvatarColor = "#8B5CF6",
                IsActive = true,
                CreatedAt = DateTime.UtcNow.AddDays(-60)
            };

            db.Users.AddRange(adminUser, analystUser, operatorUser, kevinAiUser);
            await db.SaveChangesAsync();
        }

        // Seed Categories if empty
        if (!await db.Categories.AnyAsync())
        {
            var categories = new List<Category>
            {
                new() { Name = "Keamanan", Color = "#EF4444", Description = "Ancaman siber, peretasan, ransomware, kebocoran data dan pertahanan infosec" },
                new() { Name = "Politik", Color = "#F59E0B", Description = "Kebijakan publik, regulasi pemerintah, pemilu, diplomasi dan isu tata kelola" },
                new() { Name = "Ekonomi", Color = "#10B981", Description = "Pasar keuangan, perbankan, e-commerce, pertumbuhan ekonomi dan fintech" },
                new() { Name = "Teknologi", Color = "#3B82F6", Description = "Adopsi kecerdasan buatan, jaringan 5G, komputasi awan dan inovasi digital" },
                new() { Name = "Sosial", Color = "#8B5CF6", Description = "Opini masyarakat, dinamika media sosial, edukasi digital dan gerakan warga" },
                new() { Name = "Kesehatan", Color = "#EC4899", Description = "Layanan kesehatan, rumah sakit, vaksinasi, wabah dan kebijakan kesehatan masyarakat" },
                new() { Name = "Lingkungan", Color = "#22C55E", Description = "Cuaca, iklim, energi terbarukan, kebencanaan alam dan isu lingkungan hidup" },
                new() { Name = "Bencana", Color = "#F97316", Description = "Informasi BMKG, mitigasi bencana alam, cuaca ekstrem dan peringatan dini" },
                new() { Name = "Dark Web", Color = "#64748B", Description = "Pemantauan forum tersembunyi, pasar ilegal dan ancaman aktor peretas" }
            };

            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }

        // Seed Sources if empty
        if (!await db.Sources.AnyAsync())
        {
            var newsCat = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Politik");

            var sources = new List<Source>
            {
                new() { Name = "www.cnnindonesia.com", Kind = SourceKind.News, Url = "https://www.cnnindonesia.com/rss", Country = "Indonesia", TrustScore = 0.92, CategoryId = newsCat?.Id },
                new() { Name = "www.antaranews.com", Kind = SourceKind.News, Url = "https://www.antaranews.com/rss/terkini.xml", Country = "Indonesia", TrustScore = 0.95, CategoryId = newsCat?.Id },
                new() { Name = "rss.tempo.co", Kind = SourceKind.News, Url = "https://rss.tempo.co/nasional", Country = "Indonesia", TrustScore = 0.90, CategoryId = newsCat?.Id },
                new() { Name = "www.cnbcindonesia.com", Kind = SourceKind.News, Url = "https://www.cnbcindonesia.com/news/rss", Country = "Indonesia", TrustScore = 0.91, CategoryId = newsCat?.Id },
                new() { Name = "news.detik.com", Kind = SourceKind.News, Url = "https://news.detik.com/rss", Country = "Indonesia", TrustScore = 0.89, CategoryId = newsCat?.Id },
                new() { Name = "www.okezone.com", Kind = SourceKind.News, Url = "https://www.okezone.com/rss", Country = "Indonesia", TrustScore = 0.88, CategoryId = newsCat?.Id },
                new() { Name = "www.sindonews.com", Kind = SourceKind.News, Url = "https://www.sindonews.com/feed", Country = "Indonesia", TrustScore = 0.87, CategoryId = newsCat?.Id },
                // Daerah — Jawa Barat & Banten
                new() { Name = "www.radarbandung.id", Kind = SourceKind.News, Url = "https://www.radarbandung.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.radarcirebon.id", Kind = SourceKind.News, Url = "https://www.radarcirebon.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.radarbanten.co.id", Kind = SourceKind.News, Url = "https://www.radarbanten.co.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "jabarekspres.com", Kind = SourceKind.News, Url = "https://jabarekspres.com/feed", Country = "Indonesia", TrustScore = 0.78, CategoryId = newsCat?.Id },
                // Daerah — Jawa Tengah & Jawa Timur
                new() { Name = "www.radarpekalongan.id", Kind = SourceKind.News, Url = "https://www.radarpekalongan.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.beritajatim.com", Kind = SourceKind.News, Url = "https://www.beritajatim.com/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.harianbhirawa.co.id", Kind = SourceKind.News, Url = "https://www.harianbhirawa.co.id/feed", Country = "Indonesia", TrustScore = 0.78, CategoryId = newsCat?.Id },
                // Daerah — Sumatera
                new() { Name = "redaksi.waspada.co.id", Kind = SourceKind.News, Url = "https://redaksi.waspada.co.id/v2024/feed/", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.riaupos.co", Kind = SourceKind.News, Url = "https://www.riaupos.co/feed", Country = "Indonesia", TrustScore = 0.78, CategoryId = newsCat?.Id },
                new() { Name = "sumselupdate.com", Kind = SourceKind.News, Url = "https://sumselupdate.com/feed", Country = "Indonesia", TrustScore = 0.78, CategoryId = newsCat?.Id },
                // Daerah — Indonesia Timur
                new() { Name = "www.radarmakassar.id", Kind = SourceKind.News, Url = "https://www.radarmakassar.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id },
                new() { Name = "www.radarambon.id", Kind = SourceKind.News, Url = "https://www.radarambon.id/feed", Country = "Indonesia", TrustScore = 0.80, CategoryId = newsCat?.Id }
            };

            db.Sources.AddRange(sources);
            await db.SaveChangesAsync();
        }

        // Seed Watch Keywords if empty
        if (!await db.WatchKeywords.AnyAsync())
        {
            var secCat = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Keamanan");
            var polCat = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Politik");
            var ekoCat = await db.Categories.FirstOrDefaultAsync(c => c.Name == "Ekonomi");

            var keywords = new List<WatchKeyword>
            {
                new() { Term = "ransomware", Severity = AlertSeverity.Critical, CategoryId = secCat?.Id, IsActive = true, NotifyEmail = "alert@cyberlens.id" },
                new() { Term = "bocor", Severity = AlertSeverity.Warning, CategoryId = secCat?.Id, IsActive = true, NotifyEmail = "alert@cyberlens.id" },
                new() { Term = "data breach", Severity = AlertSeverity.Critical, CategoryId = secCat?.Id, IsActive = true, NotifyEmail = "alert@cyberlens.id" },
                new() { Term = "IKN", Severity = AlertSeverity.Info, CategoryId = polCat?.Id, IsActive = true, NotifyEmail = "policy@cyberlens.id" },
                new() { Term = "rupiah", Severity = AlertSeverity.Info, CategoryId = ekoCat?.Id, IsActive = true, NotifyEmail = "econ@cyberlens.id" },
                new() { Term = "phishing", Severity = AlertSeverity.Warning, CategoryId = secCat?.Id, IsActive = true, NotifyEmail = "alert@cyberlens.id" }
            };

            db.WatchKeywords.AddRange(keywords);
            await db.SaveChangesAsync();
        }

        // Bersihkan data fiktif lama (dari versi demo sebelumnya) yang tersisa di DB — idempotent.
        await CleanupLegacyDemoDataAsync(db);
    }

    /// <summary>
    /// Menghapus sisa data dummy dari instalasi lama (era demo): post sintetis (URL pola .../item/...),
    /// graf entitas, alert fiktif, dan — bila DB terbukti berasal dari instalasi demo — seluruh
    /// log operasional lama (CrawlRun &amp; AuditLog) yang hanya berisi entri fiktif.
    /// Data hasil crawler nyata di DB baru tidak pernah disentuh.
    /// </summary>
    private static async Task CleanupLegacyDemoDataAsync(CyberLensDbContext db)
    {
        var foundDemoData = false;

        // 1. Post sintetis: URL dibuat BuildPost lama sebagai "<sourceUrl>/item/<angka>".
        var fakePosts = await db.Posts.Where(p => p.Url != null && EF.Functions.Like(p.Url, "%/item/%")).ToListAsync();
        if (fakePosts.Count > 0)
        {
            foundDemoData = true;
            db.Posts.RemoveRange(fakePosts);
            await db.SaveChangesAsync();
        }

        // 2. Graf entitas & link — seluruhnya berasal dari seed lama (tidak ada connector yang mengisinya).
        var anyNodes = await db.EntityNodes.AnyAsync();
        if (anyNodes)
        {
            foundDemoData = true;
            db.EntityNodes.RemoveRange(await db.EntityNodes.ToListAsync());
            await db.SaveChangesAsync();
        }

        // 3. Alert tanpa post terkait — alert nyata selalu dibuat dengan PostId oleh AlertMonitorService.
        var orphanAlerts = await db.Alerts.Where(a => a.PostId == null).ToListAsync();
        if (orphanAlerts.Count > 0)
        {
            foundDemoData = true;
            db.Alerts.RemoveRange(orphanAlerts);
            await db.SaveChangesAsync();
        }

        // 4. Log crawl dari simulator demo dan entri CrawlRun fiktif lain dari seed lama
        //    ("RSS: cnnindonesia.com", "RSS: antaranews.com", "DarkWeb Connector" — bukan nama connector nyata).
        var fakeRuns = await db.CrawlRuns
            .Where(r => EF.Functions.Like(r.Connector, "%Simulator%")
                        || r.Connector == "RSS: cnnindonesia.com"
                        || r.Connector == "RSS: antaranews.com"
                        || r.Connector == "DarkWeb Connector")
            .ToListAsync();
        if (fakeRuns.Count > 0)
        {
            foundDemoData = true;
            db.CrawlRuns.RemoveRange(fakeRuns);
            await db.SaveChangesAsync();
        }

        // 5. Audit log sampel (inisialisasi lama menyebut "data sampel").
        var fakeLogs = await db.AuditLogs.Where(l => l.Detail != null && EF.Functions.Like(l.Detail, "%data sampel%")).ToListAsync();
        if (fakeLogs.Count > 0)
        {
            foundDemoData = true;
            db.AuditLogs.RemoveRange(fakeLogs);
            await db.SaveChangesAsync();
        }

        // 6. DB ini berasal dari instalasi demo lama (terbukti ada jejak data fiktif di atas) —
        //    sisa CrawlRun & AuditLog lain juga berasal dari seed demo, bukan dari crawler nyata.
        //    Bersihkan agar Log Aktivitas Crawler & Audit Trail hanya berisi data asli.
        if (foundDemoData)
        {
            db.CrawlRuns.RemoveRange(await db.CrawlRuns.ToListAsync());
            db.AuditLogs.RemoveRange(await db.AuditLogs.ToListAsync());
            await db.SaveChangesAsync();
        }
    }
}
