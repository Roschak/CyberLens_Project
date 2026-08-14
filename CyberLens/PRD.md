# Product Requirements Document (PRD)
## CyberLens — Platform OSINT & Media Monitoring

| Informasi Dokumen | Detail |
| --- | --- |
| **Nama Produk** | CyberLens |
| **Versi Dokumen** | 1.0.0 |
| **Tanggal Terbit** | 14 Agustus 2026 |
| **Status** | Final Approved |
| **Penulis** | Gemini CLI |
| **Tim Pengembang** | Gravicode Studios (Dipimpin oleh Kang Fadhil) |

---

## 1. Ringkasan Eksekutif (Executive Summary)

### 1.1 Latar Belakang
Di era keterbukaan informasi saat ini, data dari berbagai sumber terbuka (Open Source Intelligence atau OSINT) seperti media sosial, forum, portal berita, blog, dan bahkan dark web mengalir dengan volume yang sangat masif dan kecepatan yang sangat tinggi. Organisasi, instansi pemerintah, dan perusahaan memerlukan alat pemantauan media yang komprehensif untuk mendeteksi ancaman, menganalisis sentimen publik, memprediksi pergerakan isu, serta mengambil keputusan strategis berdasarkan data real-time.

### 1.2 Deskripsi Produk
**CyberLens** adalah aplikasi OSINT dan Media Monitoring terintegrasi satu proses (*single-process, single-host*) berbasis .NET 10 dan Blazor Server. Aplikasi ini mengintegrasikan fungsionalitas UI interaktif, REST API, dan tiga pekerja latar belakang (*background workers*) dalam satu host aplikasi tunggal. CyberLens dirancang dengan estetika visual **Neo-Brutalism** modern, memiliki performa yang dioptimalkan, serta didukung oleh agen AI pintar asisten intelijen bernama **Bang Kevin** berbasis Microsoft Semantic Kernel.

### 1.3 Tujuan Produk
*   **Sentralisasi OSINT**: Mengumpulkan data dari berbagai kanal (RSS, Reddit, Mastodon, YouTube, Twitter/X, Facebook, Threads, TikTok, dan Dark Web) secara tersentralisasi.
*   **Analisis Cerdas**: Menyediakan analisis sentimen otomatis, ekstraksi entitas, klasifikasi topik, dan deteksi ancaman real-time.
*   **Visualisasi Interaktif**: Menyajikan visualisasi data yang kaya, termasuk grafik tren, jaringan entitas (force-directed graph), peta geospasial Leaflet, dan Globe 3D interaktif menggunakan Three.js.
*   **Asisten AI (Bang Kevin)**: Memungkinkan analis menanyakan data internal platform dan melakukan pencarian internet serta web-scraping menggunakan bahasa alami.
*   **Pelaporan Otomatis**: Mendukung pembuatan laporan otomatis maupun on-demand dalam format PDF dan Excel.

---

## 2. Arsitektur & Teknologi (Technical Stack)

Aplikasi dibangun menggunakan arsitektur monolitik modern yang sangat efisien, menggabungkan backend, frontend, dan worker dalam satu proses eksekusi.

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

### 2.1 Komponen Teknologi Utama
*   **Framework Utama**: ASP.NET Core (.NET 10) dengan Blazor Server (Global Interactive Server rendering).
*   **Penyimpanan Data (Database Agnostic)**: Entity Framework Core (EF Core 10) dengan dukungan otomatis untuk empat provider database:
    *   **SQLite** (Default untuk kemudahan setup lokal)
    *   **SQL Server** (Untuk skala enterprise)
    *   **MySQL / MariaDB** (Open-source relasional)
    *   **PostgreSQL** (Relasional performa tinggi)
*   **Penyimpanan File (`IFileStorage`)**:
    *   **Local FileSystem** (Default)
    *   **Azure Blob Storage** (Cloud Azure)
    *   **Amazon S3** (Cloud AWS)
    *   **MinIO** (Penyimpanan S3-compatible lokal)
*   **AI Engine**: **Microsoft Semantic Kernel** sebagai orkestrator LLM (mendukung OpenAI, Anthropic/Claude, Gemini, dan Ollama).
*   **Pembaruan Real-Time**: `NotificationBus` (mekanisme Pub/Sub internal dalam memori) yang mengirimkan event dari background worker ke sirkuit Blazor Server yang aktif untuk memperbarui UI tanpa perlu me-refresh halaman.
*   **Visualisasi Frontend**:
    *   **D3.js**: Grafik tren, distribusi kategori, word cloud, dan grafik visualisasi jaringan entitas.
    *   **Leaflet**: Peta geospasial interaktif menggunakan ubin OpenStreetMap/CARTO.
    *   **Three.js**: Bola Dunia 3D (3D Globe) interaktif untuk pemetaan spasial ancaman global.
*   **Desain UI**: Gaya visual **Neo-Brutalism** modern dengan kontras tinggi, garis tebal, bayangan tegas, dan dukungan penuh tema Gelap/Terang (*Dark/Light theme*).

---

## 3. Fitur Utama & Kebutuhan Fungsional (Core Features)

### 3.1 Data Collection & Crawler Ops
Sistem pengumpulan data mengintegrasikan konektor nyata (*real connectors*) ke berbagai platform sosial dan berita, berjalan secara terjadwal maupun manual melalui tombol **"Crawl sekarang"** (untuk pengguna Analyst/Admin).

| Sumber Koleksi | Tipe Data | Kredensial | Metode & Keterangan |
| --- | --- | --- | --- |
| **RSS/Atom Feeds** | Berita Online | Tanpa Kredensial | Mengurai feed XML berita real-time (contoh: Antara, Google News ID, BBC World). Feed kustom dapat ditambahkan via Settings. |
| **Reddit** | Forum Diskusi | Tanpa Kredensial | Mengakses API publik JSON (`/r/{subreddit}/new.json`) untuk mengambil thread baru. |
| **Mastodon** | Media Sosial | Tanpa Kredensial | Mengakses timeline hashtag publik pada instansi Mastodon yang dikonfigurasi. |
| **YouTube** | Media Sosial / Video | API Key | Menggunakan YouTube Data API v3 (`search.list`) berdasarkan kata kunci pencarian. |
| **Twitter / X** | Media Sosial | Bearer Token | Menggunakan X API v2 (recent search) berdasarkan kata kunci pencarian. |
| **Facebook** | Media Sosial | Page Access Token | Mengakses Page feed menggunakan Graph API Facebook. |
| **Threads** | Media Sosial | Access Token | Mengambil post pengguna melalui Threads Graph API. |
| **TikTok** | Media Sosial / Video | Client Key & Secret | Menggunakan TikTok API Resmi dengan alur client-credentials dan kueri video. |
| **Dark Web** | Forum & Marketplace | Tor Proxy / API Key | 1. **Tor Proxy**: Mengambil konten halaman `.onion` yang dikonfigurasi via SOCKS5 proxy (Tor Client lokal).<br>2. **Threat Intel Feed**: Menarik feed JSON kebocoran data di clearnet secara generik. |
| **Simulator** | Media Sosial | Tanpa Kredensial | Menghasilkan lalu lintas simulasi berkualitas tinggi ketika API media sosial nyata tidak dikonfigurasi (berguna untuk demo/evaluasi). |

### 3.2 Data Processing & Normalization
Setiap data yang berhasil ditarik oleh kolektor akan melewati pipa pemrosesan otomatis sebelum disimpan ke database:
*   **Normalisasi Konten**: Membersihkan noise HTML, karakter tidak valid, dan melakukan de-duplikasi konten menggunakan algoritma hashing konten.
*   **Analisis Sentimen**: Menggunakan modul `SentimentAnalyzer` berbasis leksikon dwibahasa (Indonesia + Inggris) dengan penanganan kata negasi (misal: "tidak bagus" dideteksi sebagai negatif).
*   **Klasifikasi Topik Otomatis**: Modul `TopicClassifier` mengklasifikasikan postingan ke dalam kategori (Keamanan, Politik, Ekonomi, Sosial, Teknologi, Hukum, dll.) berdasarkan aturan pencocokan kata kunci.
*   **Geocoding Ringan**: `SimpleGeocoder` memetakan teks lokasi atau penyebutan kota dalam konten menjadi koordinat geografis (latitude/longitude) sehingga data dapat divisualisasikan pada peta.

### 3.3 Visualisasi & Intelijen (Intelligence Visualization)
CyberLens menyediakan beberapa halaman visualisasi interaktif tingkat tinggi:
1.  **Dashboard Utama**: Menyajikan statistik volume koleksi hari ini/7 hari/total, tren volume kategori, donat analisis sentimen, kata kunci tren dalam bentuk *word cloud*, dan distribusi topik. UI diperbarui secara real-time saat crawler bekerja.
2.  **Live Feed**: Aliran data mentah hasil koleksi secara real-time dengan filter teks, jenis sentimen, dan tipe sumber data.
3.  **Tren & Prediksi**: Analisis tren dan volume postingan dilengkapi dengan fitur **Prediksi berbasis AI** (peramalan volume 7 hari ke depan menggunakan regresi linear, ditampilkan dengan garis putus-putus pada grafik).
4.  **Jaringan Entitas (Entity Network)**: Grafik hubungan antar-entitas (orang, organisasi, tagar, lokasi, akun) menggunakan force-directed graph D3.js. Ukuran simpul (node) ditentukan berdasarkan frekuensi kemunculan dalam pemberitaan.
5.  **Peta Geospasial (GeoMap)**: Peta Leaflet interaktif yang menampilkan penanda lingkaran lokasi berita. Warna penanda diwarnai berdasarkan sentimen (hijau = positif, kuning = netral, merah = negatif) dan ukuran lingkaran berdasarkan volume postingan di lokasi tersebut.
6.  **Globe 3D Intelijen (3D Globe)**: Menggunakan Three.js/WebGL untuk memetakan OSINT secara spasial dengan layer-layer interaktif yang dapat diaktifkan melalui header:
    *   **Layer Sentimen (Heatmap)**: Titik berpendar aditif (hijau/kuning/merah) yang menunjukkan intensitas interaksi.
    *   **Layer Sumber**: Penanda berbentuk kerucut berwarna-warni yang mewakili jenis sumber data di permukaan bumi.
    *   **Layer Peristiwa**: Gelembung transparan yang menyatukan peristiwa-peristiwa penting berdasarkan wilayah geografis.
    *   **Layer Ancaman**: Batang merah vertikal yang berdenyut, mewakili ancaman siber yang terdeteksi dari dark web atau kategori keamanan.
    *   **Timeline Slider**: Fitur kontrol waktu (dengan tombol *Play/Pause*) untuk menyaksikan evolusi kronologis data intelijen di bola dunia.

### 3.4 Asisten AI Pintar "Bang Kevin" (AI Assistant & Chat Bot)
Halaman obrolan asisten AI yang cerdas dan kaya fitur untuk membantu analis mengekstrak wawasan dari platform.

*   **Multi-Session Management**: Pengguna dapat membuat, menghapus, atau mengatur ulang sesi percakapan. Judul sesi akan diubah secara otomatis berdasarkan pesan pertama yang dikirim.
*   **Grounded QA via Kernel Functions (Tools)**: Bang Kevin tidak hanya menjawab pertanyaan secara generik, melainkan terhubung langsung ke database internal CyberLens dan internet via Semantic Kernel:
    *   *Utility Plugins*: Mengetahui tanggal/waktu saat ini (UTC + WIB), kalkulator aritmatika, dan penghitung selisih hari.
    *   *Web Plugins*: Pencarian internet (`SearchInternet` via Tavily API), pengambilan teks halaman web (`ScrapePage`), dan pembaca file dari URL (`ReadFileFromUrl`).
    *   *OSINT Data Plugins*: Mengakses statistik internal sistem langsung lewat obrolan: ringkasan platform (`GetOverview`), sebaran sentimen (`GetSentiment`), topik tren (`GetTrendingTopics`), pencarian postingan (`SearchPosts`), statistik kategori (`GetCategoryStats`), kata kunci pantauan (`GetWatchKeywords`), alert terbaru (`GetRecentAlerts`), data sumber (`GetSources`), prediksi tren (`PredictTrend`), dan entitas teratas (`GetTopEntities`).
*   **Lampiran Berkas (Attachments)**: Analis dapat mengunggah gambar (diproses secara multimodal oleh LLM yang mendukung seperti Claude) atau dokumen (teks dokumen disematkan langsung dalam konteks pesan). Berkas disimpan secara otomatis ke dalam backend penyimpanan aktif (`IFileStorage`).
*   **Markdown Rendering**: Jawaban dari asisten dirender dengan indah ke HTML, mendukung tabel, blok kode, tautan, serta elemen media (gambar/video/audio).

### 3.5 AI-Based Analytics (Intelligence Briefing)
Fitur generasi ringkasan laporan intelijen komparatif otomatis dari data yang ditarik crawler dalam rentang waktu tertentu. Laporan mencakup:
*   Ringkasan Situasi (*Executive Summary*)
*   Tingkat Risiko Keamanan (*Risk Level*)
*   Temuan Utama (*Key Findings*)
*   Rekomendasi Aksi (*Actionable Recommendations*)
*   Daftar Ancaman Utama (*Top Threats*)
*   Prospek Masa Depan (*Outlook*)
*   Grafik pendukung intelijen.

### 3.6 Alerting & Real-Time Notification
*   **Kata Kunci Pantauan (Watch Keywords)**: Pengguna dengan peran Analyst/Admin dapat mendaftarkan kata kunci yang ingin dipantau ketat beserta tingkat keparahannya (*severity*: Info, Warning, Critical).
*   **Pemantau Latar Belakang (`AlertMonitorService`)**: Worker yang berjalan di latar belakang mendeteksi postingan baru yang cocok dengan kata kunci pantauan secara real-time dan menerbitkan data `Alert`.
*   **Pemberitahuan UI Instan**: Lonceng notifikasi pada topbar dan toast notifikasi akan muncul secara real-time bagi semua analis yang aktif tanpa perlu me-refresh halaman (didorong oleh `NotificationBus`).

### 3.7 Pelaporan (Reporting)
*   **Ekspor Dokumen**: Mendukung pembuatan laporan dokumen profesional berformat PDF (menggunakan QuestPDF) dan Excel (menggunakan ClosedXML) secara *on-demand*.
*   **Penjadwalan Laporan (`ReportSchedulerService`)**: Worker latar belakang yang secara otomatis membuat laporan berkala (Harian, Mingguan, Bulanan) berdasarkan konfigurasi sistem dan mengirimkannya ke folder arsip laporan.

### 3.8 Administration, Security & Audit Trail
*   **Authentication**: Autentikasi berbasis Cookie dengan enkripsi password menggunakan algoritma **PBKDF2-SHA256** (100.000 iterasi dengan garam/salt unik per pengguna).
*   **Authorization (Role-Based Access Control - RBAC)**:
    *   **Viewer**: Membaca semua halaman visualisasi intelijen dan operasi.
    *   **Analyst**: Kemampuan Viewer + mengelola kata kunci, menginisiasi crawl manual, dan mengunduh/membuat laporan.
    *   **Admin**: Kemampuan Analyst + mengelola pengguna, melihat Audit Trail, dan mengubah konfigurasi global.
*   **Audit Trail**: Log aktivitas yang merekam setiap tindakan sensitif (login/logout, perubahan konfigurasi, manajemen pengguna, pembuatan laporan, manipulasi kata kunci) ke dalam tabel `AuditLog` yang bersifat append-only.
*   **External REST API Integration**: Menyediakan endpoints `/api/v1` untuk integrasi dengan sistem eksternal (terdokumentasi secara interaktif melalui Swagger di `/swagger`). Akses API diamankan menggunakan header `X-Api-Key` dan dapat dinonaktifkan sepenuhnya di menu Settings.

---

## 4. Spesifikasi Database & Entitas (Database Schema)

Database EF Core memetakan entitas-entitas utama berikut:

1.  **AppUser**: Data akun pengguna, peran (Role), hash password PBKDF2, dan salt.
2.  **Source**: Entitas sumber data media sosial, portal berita, RSS feed, dll.
3.  **Category**: Kategori klasifikasi isu (misal: Politik, Keamanan, Ekonomi).
4.  **Post**: Postingan/berita hasil crawling, menyimpan konten mentah, hash de-duplikasi, skor sentimen, timestamp, koordinat lokasi, dan relasi kategori.
5.  **PostMedia**: Berkas media yang melekat pada postingan (gambar/video/audio).
6.  **WatchKeyword**: Daftar kata kunci pantauan real-time beserta tingkat keparahannya.
7.  **Alert**: Hit kata kunci yang memicu peringatan real-time.
8.  **EntityNode & EntityLink**: Pemetaan relasi antar entitas untuk visualisasi graph jaringan.
9.  **ChatSession, ChatMessage, & ChatAttachment**: Data obrolan multi-sesi bersama Bang Kevin beserta berkas unggahan lampiran obrolan.
10. **AuditLog**: Log audit aktivitas sensitif pengguna sistem.
11. **ReportRecord**: Rekaman arsip laporan terbuat (PDF/Excel) beserta status dan link unduhan.

---

## 5. Kebutuhan Non-Fungsional (Non-Functional Requirements)

*   **Usability & UI/UX**: Desain Neo-Brutalism dengan kontras tinggi yang memudahkan keterbacaan data intelijen dalam situasi operasional tinggi. Mendukung peralihan mode gelap dan terang instan yang tersimpan di browser pengguna.
*   **Performance & Efficiency**:
    *   Aplikasi tunggal yang ringan tanpa overhead komunikasi antar-service yang rumit (satu proses host).
    *   EF Core dioptimalkan dengan siklus hidup DbContext yang pendek (`IDbContextFactory`), mencegah kebocoran memori di Blazor Server dan aman untuk eksekusi multi-thread background worker.
    *   Visualisasi 3D Globe dibatasi maksimal menampilkan ~700 titik spasial terbaru untuk menjaga performa rendering GPU pada peramban pengguna.
*   **Security & Path Sanitization**:
    *   Pembersihan jalur file (*path sanitization*) ketat terhadap serangan *directory traversal* (`..` ditolak dan dinormalisasi) pada semua backend penyimpanan file (`FileSystemStorage`, S3, dll).
    *   Penyimpanan rahasia API key disimpan dalam file terpisah `config/cyberlens.settings.json` yang tidak di-commit ke repositori Git.
    *   Redaksi sebagian nama/identifier aktor pada modul Dark Web demi kerahasiaan dan kepatuhan privasi data.

---

## 6. Matriks Hak Akses (Authorization Matrix)

| Fitur / Halaman | Keamanan Halaman | Viewer | Analyst | Admin |
| --- | --- | :---: | :---: | :---: |
| Dashboard, Live Feed, Tren, Network, Peta | `[Authorize]` | ✔ | ✔ | ✔ |
| Globe 3D Intelijen, Dark Web, Alerts | `[Authorize]` | ✔ | ✔ | ✔ |
| Bang Kevin (Asisten AI) | `[Authorize]` | ✔ | ✔ | ✔ |
| Kelola Kata Kunci (Watch Keywords) | `[Authorize]` | ❌ | ✔ | ✔ |
| Jalankan Crawl Manual ("Crawl sekarang") | `[Authorize]` | ❌ | ✔ | ✔ |
| Kelola & Buat Laporan | `[Authorize]` | ❌ | ✔ | ✔ |
| Kelola Akun Pengguna | `[Authorize(Roles="Admin")]` | ❌ | ❌ | ✔ |
| Lihat Audit Trail (Aktivitas Log) | `[Authorize(Roles="Admin")]` | ❌ | ❌ | ✔ |
| Pengaturan Global (Settings Panel) | `[Authorize(Roles="Admin")]` | ❌ | ❌ | ✔ |

---

## 7. Panduan Pengerasan Produksi (Production Hardening Checklist)

Sebelum meluncurkan CyberLens ke lingkungan produksi, pastikan kepatuhan terhadap standar keamanan berikut:
1.  **Enkripsi Transportasi**: Pastikan aplikasi disajikan secara ketat melalui protokol **HTTPS** dan pastikan fitur `Hsts` (HTTP Strict Transport Security) dalam posisi aktif.
2.  **Kredensial Default**: Hapus atau ubah seluruh kata sandi akun demo bawaan (`admin`, `supervisor`, `analyst`, `viewer`).
3.  **Kunci Keamanan API**: Ubah nilai default `Api.ApiKey` (`cyberlens-demo-key`) ke string acak tingkat tinggi yang aman di pengaturan.
4.  **Isolasi Konfigurasi**: Tempatkan berkas `config/cyberlens.settings.json` di tempat aman dengan pembatasan hak akses baca-tulis file system atau manfaatkan secrets manager.
5.  **Migrasi Database**: Gunakan fungsionalitas EF Core Migrations daripada `EnsureCreated()` untuk pembaruan schema database produksi secara aman tanpa risiko kehilangan data.
6.  **Pembatasan Outbound**: Batasi akses keluar (outbound network) untuk mesin crawler agar hanya menghubungi daftar API resmi sosial media, feed RSS, dan endpoint Tor proxy yang terpercaya demi mencegah risiko eksploitasi URL jahat.
7.  **Reverse Proxy**: Letakkan aplikasi di belakang reverse proxy tangguh (misalnya Nginx, IIS, atau Cloudflare) yang dilengkapi konfigurasi pembatasan laju permintaan (*rate limiting*) untuk melindungi endpoint REST API publik dari serangan DDoS atau brute-force.
