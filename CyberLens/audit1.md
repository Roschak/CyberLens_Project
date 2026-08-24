# 🔍 Audit CyberLens — Sumber Data, Kategori, Bobot, dan Mode Crawler (Dalam Negeri / Luar Negeri)

**Tanggal:** 18 Agustus 2026
**Status Build:** ✅ 0 error (sebelumnya 36 error di baseline)
**Status Crawler:** ✅ Terverifikasi jalan (smoke test)

---

## Ringkasan Eksekutif

Audit ini menjawab 5 pertanyaan: dari mana data chart donat & kategori, kenapa ada media luar (BBC), dari mana bobot/skor dan apakah konsisten, serta implementasi mode crawler **Dalam Negeri / Luar Negeri**. Ditemukan **2 masalah fatal** yang membuat "semuanya tidak jalan":

1. **Repo git tidak bisa di-build** (36 error kompilasi) — refactor setengah jadi.
2. **`.gitignore` menelan folder sumber `Data/`** (Entities.cs, DbSeeder.cs, SampleContent.cs, CyberLensDbContext.cs) karena pola `src/CyberLens/data/` (huruf kecil) cocok dengan `src/CyberLens/Data/` (huruf besar) di Windows yang case-insensitive → **seluruh lapisan database tidak pernah masuk git**.

Keduanya sudah diperbaiki dan diverifikasi.

---

## 1. Chart Donat & Kategorinya Dari Mana?

| Chart | Sumber data | Lokasi kode |
|---|---|---|
| **Donat "Sebaran Sentimen"** (Dashboard & AI Analytics) | `AnalyticsService.GetSentimentBreakdownAsync(7)` — hitung post di DB 7 hari terakhir, dikelompokkan label `positive/neutral/negative` | `Services/Analysis/AnalyticsService.cs`, `Components/Pages/Home.razor`, `Components/Pages/AiAnalytics.razor` |
| **Kategori** (bar chart) | Tabel DB `Categories` (di-seed `DbSeeder`) + `TopicClassifier.Classify` (keyword matching) saat item disimpan | `Data/DbSeeder.cs`, `Services/Analysis/TopicClassifier.cs` |
| **Donat "Sukses/Gagal"** (Crawler Ops) | Log `CrawlRuns` — hasil tiap connector per pass | `Components/Pages/Crawler.razor`, `Services/Collection/CrawlLogService.cs` |

**Alur datanya:**
1. Crawler (jadwal 45 detik atau tombol "Crawl sekarang") mengambil item dari feed RSS + media sosial + simulator.
2. Setiap item dinormalisasi → dedup (hash URL) → **skor sentimen** → **klasifikasi kategori** → tag → geokode → disimpan ke DB.
3. Chart membaca agregat dari DB — bukan dari sumber luar.

### ⚠️ Bug "data bobrok" yang ditemukan & diperbaiki

`TopicClassifier` bisa mengembalikan kategori **"Kesehatan"** dan **"Lingkungan"**, tetapi kedua kategori itu **tidak ada di seed DB** (`DbSeeder` hanya punya: Keamanan, Politik, Ekonomi, Teknologi, Sosial, Bencana, Dark Web). Akibatnya post yang terklasifikasi ke kategori tersebut tersimpan **tanpa kategori** (`Category = null`) → hilang dari chart kategori / masuk "Lainnya".

**Perbaikan:**
- `CollectorService` sekarang **otomatis membuat kategori** yang hilang saat penyimpanan (tidak ada post yang dibuang diam-diam).
- `DbSeeder` menambahkan kategori **Kesehatan** (`#EC4899`) dan **Lingkungan** (`#22C55E`) untuk instalasi baru.

---

## 2. Kenapa Ada Media Luar (BBC)?

Penyebabnya ada di **konfigurasi default** + **tidak ada filter negara**:

1. **Default `RssFeeds`** berisi `https://feeds.bbci.co.uk/news/world/rss.xml` (BBC World — bahasa Inggris) di samping Antara & Google News ID.
2. **Reddit default** memantau sub `worldnews` (internasional).
3. **Mastodon default** pakai instance `mastodon.social` (internasional).
4. Source baru yang dibuat crawler selalu `Country = "Global"` — tidak pernah "Indonesia".

Jadi post BBC masuk sebagai post biasa dan ikut terhitung di semua chart. **Sudah diperbaiki** melalui mode crawler (lihat bagian 4).

---

## 3. Bobot/Skor Dari Mana & Apakah Konsisten?

### Skor sentimen (`SentimentScore`)

- Dihitung oleh `SentimentAnalyzer.Analyze()` — **lexicon-based** (daftar kata positif/negatif bahasa Indonesia + Inggris dengan penanganan negasi sederhana).
- Skor dinormalisasi ke rentang **-1..+1**, label: `positive` (> 0.15), `negative` (< -0.15), `neutral`.
- **Deterministik**: teks yang sama → skor yang sama persis. Skor dihitung **sekali** saat item masuk, disimpan di kolom DB, dan chart membacanya dari DB → **stabil antar refresh**.
- Contoh nyata hasil crawl: `positive 28, neutral 287, negative 104` (7 hari).

### Kategori (`TopicClassifier`)

- Keyword-based, juga deterministik. Kategori dipilih dari jumlah kemunculan kata kunci dalam teks.

### Apakah angka sama setiap crawler jalan? **Tidak — dan itu wajar, kecuali satu hal:**

| Sumber | Perilaku antar run |
|---|---|
| Feed RSS / media sosial | Hanya **item baru** yang masuk (dedup hash URL) — tidak dobel |
| **Simulator demo** ⚠️ | `SimulateSocialStreams = true` (**default ON**) memakai `new Random()` tanpa seed → tiap run menghasilkan **1–3 post sintetis acak** → DB & chart terus bertambah dengan data palsu |

**Rekomendasi:** matikan **"Simulasikan stream media sosial (demo)"** di Settings jika ingin data 100% nyata. Data sintetis juga berasal dari `DbSeeder` (~1.800 post sampel saat DB pertama dibuat) dan `SampleContent.Templates`.

---

## 4. Mode Crawler: Dalam Negeri / Luar Negeri ✅

### Yang diminta

- **Dalam Negeri** → crawler hanya mengambil berita/percakapan Indonesia.
- **Luar Negeri** → mengambil berita global juga (Indonesia + dunia).

### Yang diimplementasikan

| Perubahan | File |
|---|---|
| Enum `CrawlerMode { DalamNegeri, LuarNegeri }` + properti `Crawler.Mode` (default `DalamNegeri`) | `Models/AppConfig.cs` |
| **Filter mode di collector**: mode Dalam Negeri → feed media luar di-skip berdasarkan daftar host asing (BBC dkk) + semua item difilter **hanya bahasa Indonesia**; Luar Negeri → semua masuk | `Services/Collection/CollectorService.cs` |
| Detektor bahasa baru (Indonesia vs Inggris) untuk item yang di-crawl | `Services/Analysis/LanguageDetector.cs` (baru) |
| Deteksi bahasa di Reddit (sebelumnya hardcoded `"en"` — padahal /r/indonesia berbahasa Indonesia) | `Services/Collection/Social/RedditConnector.cs` |
| `Country` source baru: `"Indonesia"` bila item berbahasa Indonesia, selain itu `"Global"` | `Services/Collection/CollectorService.cs` |
| UI pemilih mode di Settings → Crawler → "Cakupan berita" | `Components/Pages/Settings.razor` |
| Dokumentasi mode | `docs/crawler.md` |

**Penting:** filter diterapkan di `CollectorService` sehingga **jadwal & tombol "Crawl sekarang" berperilaku identik**. Filter hanya memengaruhi item baru — data lama di DB tidak disentuh.

### Verifikasi smoke test (nyata)

Dijalankan aplikasi dengan mode **Dalam Negeri**:

- ❌ **BBC tidak di-fetch** (host `feeds.bbci.co.uk` di-skip).
- ✅ Antara News & Google News ID masuk (masing-masing ~24–25 post baru).
- ✅ Mastodon `#OSINT`/`#cybersecurity` (bahasa Inggris) **terbuang**; hanya `#Indonesia` yang tersimpan (2 post).
- ✅ Source baru tercatat `Country = "Indonesia"` untuk konten Indonesia.
- ✅ 0 post mengandung kata "bbc".

---

## 5. Temuan Kritis: Kenapa "Semuanya Tidak Jalan"

### 5a. Repo tidak bisa di-build (36 error di baseline)

Kompilasi `git stash` (state bersih) menghasilkan **36 error** — pola refactor yang belum selesai. Error terkelompok:

| Kelompok error | Penyebab | Perbaikan |
|---|---|---|
| `SourceKind.SocialMedia/Blog/Official` tidak ada (8 file) | Nilai enum hilang | Ditambahkan ke `SourceKind` |
| `UserRole.Viewer` tidak ada (Users.razor) | Nilai enum hilang | Ditambahkan ke `UserRole` |
| `WatchKeyword.NotifyRealtime` & `CreatedById` tidak ada | Field model hilang | Ditambahkan ke entitas `WatchKeyword` |
| `AuditLog.CreatedAt` tidak ada (Audit.razor) | Halaman pakai `CreatedAt`, entitas punya `Timestamp` | Halaman diperbaiki memakai `Timestamp` |
| `MediaKind` vs `MediaType` (enum kembar) | Dua enum identik | `MediaKind` dihapus, semua memakai `MediaType` |
| `SampleContent.PostTemplate` kurang argumen `Location` (17 template) | Template belum selesai | Argumen lokasi ditambahkan (sesuai koordinat) |
| `SampleContent.Cities` tidak ada (SimpleGeocoder) | Referensi salah nama | Memakai `SampleContent.Locations` |

**Hasil akhir:** `dotnet build` → **0 error** (hanya 2 warning NU1903 yang sudah ada sebelumnya).

### 5b. `.gitignore` menelan folder sumber `Data/` (paling fatal)

- Pola `.gitignore`: `src/CyberLens/data/` (huruf kecil) — dimaksudkan untuk folder runtime DB.
- Di Windows (case-insensitive), pola itu ikut meng-ignore folder **sumber** `src/CyberLens/Data/`:
  - `git ls-files` → **kosong** untuk `Entities.cs`, `SampleContent.cs`, `DbSeeder.cs`, `CyberLensDbContext.cs`.
  - `git check-ignore` → mengonfirmasi keempatnya ter-ignore.
- Akibatnya: **seluruh lapisan database tidak pernah masuk commit** → siapa pun yang clone repo ini mendapat kode yang tidak bisa dikompilasi → crawler & semua halaman mati.

**Perbaikan** (`.gitignore`):
- Pola diubah menjadi `src/CyberLens/data/cyberlens.db` (spesifik file) + komentar peringatan.
- Hasil: `Data/*.cs` kini terlihat git (untracked), `cyberlens.db` runtime tetap ter-ignore.
- File `.cs` yang masih ter-ignore hanya artefak `obj/` (generated).

---

## Daftar File yang Diubah

| File | Perubahan |
|---|---|
| `.gitignore` | Perbaiki pola ignore agar tidak menelan folder sumber `Data/` |
| `Models/AppConfig.cs` | Enum `CrawlerMode` + `Crawler.Mode` (sudah ada sebelum audit, kini dipakai) |
| `Services/Collection/CollectorService.cs` | Filter mode Dalam/Luar Negeri, auto-create kategori, `Country` Indonesia, `LanguageDetector` |
| `Services/Analysis/LanguageDetector.cs` | **Baru** — deteksi bahasa Indonesia vs Inggris |
| `Services/Collection/Social/RedditConnector.cs` | Deteksi bahasa (bukan hardcoded "en") |
| `Data/Entities.cs` | Tambah nilai enum (`SocialMedia`, `Blog`, `Official`, `Viewer`), hapus `MediaKind`, tambah `WatchKeyword.NotifyRealtime` & `CreatedById` |
| `Data/SampleContent.cs` | Perbaiki 17 template (argumen `Location`), lokasi sesuai koordinat |
| `Data/DbSeeder.cs` | Tambah kategori `Kesehatan` & `Lingkungan` |
| `Services/Collection/Social/ISocialConnector.cs` | `MediaKind` → `MediaType` |
| `Services/Collection/Social/YouTubeConnector.cs` | `MediaKind.Video` → `MediaType.Video` |
| `Services/Collection/Social/SimpleGeocoder.cs` | `SampleContent.Cities` → `SampleContent.Locations` |
| `Components/Pages/Feed.razor` | `MediaKind.Image` → `MediaType.Image` |
| `Components/Pages/Audit.razor` | `AuditLog.CreatedAt` → `Timestamp` |
| `Components/Pages/Settings.razor` | UI pemilih mode "Cakupan berita" |
| `docs/crawler.md` | Dokumentasi mode & filter |

---

## Cara Menjalankan

```bash
cd CyberLens/src/CyberLens
dotnet run
```

- DB (`cyberlens.db`) & `config/cyberlens.settings.json` dibuat otomatis saat pertama kali jalan (folder runtime, ter-ignore).
- Login default: `admin` / `admin`.

## Yang Perlu Dilakukan Pengguna

1. **Commit wajib menyertakan folder `src/CyberLens/Data/`** — sekarang sudah terlihat di `git status` sebagai untracked; tanpa itu repo tetap rusak bagi siapa pun yang clone.
2. Pertimbangkan **mematikan Simulator demo** di Settings agar dashboard hanya menampilkan data nyata.
3. Untuk mode **Dalam Negeri**, feed luar negeri (BBC dkk) di daftar RSS bisa dihapus manual — filter host sudah otomatis menanganinya.

---

*Dibuat oleh audit otomatis CyberLens. Build: 0 error. Crawler: terverifikasi berjalan dengan mode Dalam Negeri.*
