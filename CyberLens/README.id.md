# CyberLens

> Platform OSINT (Open Source Intelligence) & Media Monitoring — mengumpulkan, memproses, menganalisis, dan memvisualisasikan informasi sumber terbuka secara real-time, dilengkapi asisten intelijen AI.

**English?** Lihat [README.md](README.md).

Dibangun dengan **Blazor Server (.NET 10)**, **D3.js**, dan **Semantic Kernel**, dengan desain **neo-brutalism** modern beserta tema terang/gelap.

![Dashboard CyberLens](docs/images/dashboard.png)

---

## Fitur

- **Pengumpulan data** — crawler **nyata & langsung** dengan framework connector: RSS/Atom (Antara, Google News ID, BBC World), **Reddit** dan **Mastodon** (nyata, tanpa key), plus **YouTube, Twitter/X, Facebook, Threads, TikTok** via API resmi (aktifkan + isi kredensial di Pengaturan), serta stream **simulasi** opsional untuk demo. Berjalan **terjadwal** (background) atau **manual** ("Crawl sekarang"). Setiap item dinormalisasi, dedup berdasarkan hash konten, diberi skor sentimen, diklasifikasi otomatis, diberi tag, dan digeokode.
- **Dashboard Crawler Ops** — statistik & log aktivitas pengumpulan dalam bentuk chart + tabel yang bisa difilter: jumlah run, item terkumpul, success rate, durasi rata-rata; chart item per hari, sukses/gagal, item per connector, dan lokasi teratas; filter berdasarkan periode, connector/sumber, status, dan trigger. **Indikator status crawler** (classification strip + banner) menunjukkan apakah crawler sedang berjalan, siaga, atau nonaktif.
- **Pemrosesan** — analisis sentimen berbasis leksikon multi-bahasa (ID/EN) dengan penanganan negasi, klasifikasi topik berbasis kata kunci (Politik, Ekonomi, Keamanan, Teknologi, Sosial, Kesehatan, Lingkungan).
- **Analisis & intelijen** — statistik dashboard, analisis tren, pemantauan kata kunci real-time, analisis jaringan entitas, analisis geospasial, dan **prediksi berbasis AI** (forecast volume regresi linear).
- **AI Analytics** — **brief intelijen yang dihasilkan LLM** dari data hasil crawl: ringkasan eksekutif, penilaian risiko, temuan kunci, rekomendasi, ancaman utama, dan prospek 7 hari, dilengkapi chart pendukung. Berjalan di provider yang Anda konfigurasi (OpenAI / Anthropic / Gemini / Ollama).
- **Visualisasi** — dashboard interaktif D3.js: grafik tren dengan prediksi, donat sentimen, batang kategori, word cloud, graf jaringan entitas (force-directed), dan peta geospasial **Leaflet** dengan tile peta asli.
- **Globe 3D intelijen** — Bumi interaktif **Three.js / WebGL** yang memetakan OSINT secara spasial: heatmap sentimen (merah = negatif, hijau = positif, kuning = netral), marker geolokasi sumber, gelembung klaster peristiwa (ukuran = intensitas), overlay timeline (putar evolusi data seiring waktu), dan layer threat intelligence — semua bisa di-toggle, dengan seret untuk memutar dan scroll untuk zoom.
- **Alerting & pelaporan** — alert kata kunci real-time (toast + lonceng notifikasi), laporan otomatis terjadwal (harian/mingguan/bulanan), serta ekspor PDF (QuestPDF) / Excel (ClosedXML).
- **Kolaborasi & keamanan** — multi-user dengan akses berbasis peran (Viewer / Analyst / Admin), hashing password PBKDF2, audit trail, autentikasi cookie.
- **Asisten AI "Bang Kevin"** — chat multi-sesi (buat / hapus / reset) dengan **contoh prompt yang bisa diklik**, lampiran gambar & dokumen, render Markdown (tabel, media, kode). Dibangun di atas Semantic Kernel dengan **provider yang bisa dipilih: OpenAI, Anthropic, Gemini, Ollama**, serta kernel function untuk pencarian internet Tavily, scraping halaman, membaca file dari URL, cek tanggal/waktu, kalkulasi matematika, dan query ke data OSINT platform.
- **Pemantauan dark web** dan **REST API** (Minimal API + Swagger) untuk integrasi eksternal.

## Teknologi

| Lapisan | Teknologi |
|---------|-----------|
| UI | Blazor Server (.NET 10, Interactive Server), D3.js v7 + d3-cloud, Three.js r160 (globe 3D) |
| Desain | Neo-brutalism, Bricolage Grotesque + IBM Plex Mono + Inter Tight, terang/gelap |
| Data | EF Core 10 — **SQLite / SQL Server / MySQL / PostgreSQL** |
| Storage | **FileSystem / Azure Blob / Amazon S3 / MinIO** |
| AI | Microsoft Semantic Kernel — OpenAI / Anthropic / Gemini / Ollama |
| Pelaporan | QuestPDF (PDF), ClosedXML (Excel) |
| API | ASP.NET Minimal API + Swashbuckle (Swagger) |

## Cara menjalankan

Membutuhkan **.NET 10 SDK**.

```bash
cd src/CyberLens
dotnet run
```

Buka URL yang tercetak (mis. `http://localhost:5009`). Saat pertama dijalankan, database dibuat dan diisi data sampel secara otomatis (default SQLite — tanpa konfigurasi).

### Akun demo

Password sama dengan username.

| Username | Peran | |
|----------|-------|--|
| `admin` | Admin | akses penuh |
| `supervisor` | Admin | akses penuh |
| `analyst`, `analyst2` | Analyst | intel + operasi |
| `viewer`, `viewer2` | Viewer | hanya baca |

## Konfigurasi

**Seluruh konfigurasi operasional** (provider database & connection string, backend storage, provider AI & API key, key Tavily, crawler, alerting, API key REST, pelaporan) tersimpan di **`config/cyberlens.settings.json`** dan **dapat diubah langsung dari aplikasi** melalui halaman **Pengaturan** (khusus Admin). `appsettings.json` hanya berisi logging. Perubahan provider database dan storage berlaku setelah restart; perubahan AI, crawler, kata kunci, dan API berlaku langsung.

Lihat [docs/configuration.md](docs/configuration.md) untuk setiap pengaturan.

## REST API

Aktif secara default di `/api/v1`, terdokumentasi di `/swagger`. Kirim API key pada header `X-Api-Key` (default `cyberlens-demo-key`, bisa diubah di Pengaturan).

```bash
curl -H "X-Api-Key: cyberlens-demo-key" http://localhost:5009/api/v1/stats
```

Lihat [docs/api.md](docs/api.md) untuk semua endpoint.

## Dokumentasi

Dokumentasi lengkap ada di [`docs/`](docs/):

- [Arsitektur](docs/architecture.md)
- [Instalasi](docs/installation.md)
- [Konfigurasi](docs/configuration.md)
- [Provider database](docs/database.md)
- [Backend storage](docs/storage.md)
- [Pengumpulan data & Crawler Ops](docs/crawler.md)
- [AI Analytics](docs/ai-analytics.md)
- [Globe 3D intelijen](docs/globe.md)
- [REST API](docs/api.md)
- [Bang Kevin (asisten AI)](docs/chatbot.md)
- [Panduan pengguna](docs/user-guide.md)
- [Keamanan](docs/security.md)

## Catatan

- Stream media sosial simulasi dan seluruh data sampel bersifat **fiktif**, untuk demonstrasi. Nonaktifkan di Pengaturan dan tambahkan feed RSS asli (atau integrasikan API media sosial nyata) untuk penggunaan produksi.
- Isi API key provider AI yang valid di Pengaturan untuk mengaktifkan Bang Kevin.

## Kredit

Dibuat oleh **Gravicode Studios**, dipimpin oleh **Kang Fadhil**.

## Lisensi

Disediakan apa adanya untuk demonstrasi dan penggunaan internal.
