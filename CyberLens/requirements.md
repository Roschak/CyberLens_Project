nama: CyberLens



deskripsi:

aplikasi OSINT (Open Source Intelligence) / Media Monitoring dengan fitur:



* Data Collection

  * Crawling dari berbagai sumber: media sosial, berita online, blog, forum, website resmi.
  * Dukungan multi-format (teks, gambar, video, metadata).
  * Integrasi dengan API publik (Twitter, Facebook, YouTube, Threads, TikTok, dll.).



* Data Processing

  * Normalisasi data (membersihkan noise, duplikasi).
  * Dukungan multi-bahasa, termasuk bahasa informal atau campuran (misalnya Arablish/Arabizi).
  * Klasifikasi otomatis berdasarkan kategori (politik, ekonomi, keamanan, dll.).



* Analysis \& Intelligence

  * Sentiment analysis: positif, negatif, netral.
  * Trend analysis: topik yang sedang naik daun.
  * Keyword monitoring: memantau kata kunci tertentu secara real-time.
  * Network analysis: hubungan antar akun, sumber, atau entitas.
  * Geospatial analysis: lokasi sumber informasi.



* Visualization

  * Dashboard interaktif dengan grafik tren, peta panas, timeline.
  * Word cloud untuk kata kunci populer.
  * Analisis jaringan (graph visualization).



* Alerting \& Reporting

  * Notifikasi real-time untuk kata kunci atau isu tertentu.
  * Laporan otomatis (harian, mingguan, bulanan).
  * Export ke PDF/Excel untuk dokumentasi.



* Collaboration \& Security

  * Multi-user dengan role-based access.
  * Audit trail untuk aktivitas pengguna.
  * Penyimpanan data di server lokal/cloud dengan enkripsi.



* Chat Bot Pelayanan Informasi

  * Nama 'Bang Kevin'
  * Chat Page dengan tampilan yang keren, multi session (create/delete), reset session, bisa attach gambar (diupload lalu url-nya di jadikan image content) dan dokumen (di upload dan disertakan linknya ke text message).
  * System Prompt (persona), temperature, model dan setting lainnya di simpan di appsetting
  * Menggunakan Semantic Kernel Library dengan dukungan model: Open AI, Anthropic, Gemini, Ollama (bisa pilih)
  * Tambahkan beberapa common functions (kernel functions) yang diperlukan termasuk query ke tavily (search internet), scrap page url, baca file dari url, cek tanggal, Waktu, math calculation, dan beberapa function yang diperlukan lainnya
  * Tambahkan functions untuk query data ke database dan fungsi-fungsi analisa yang dimiliki untuk mengetahui berbagai informasi
  * Bisa render chat thread dengan mark down dengan baik ke html (baik table, media (image, video, audio), code, dan lainnya dengan baik)



Fitur Tambahan

* AI-based prediction: memprediksi perkembangan isu berdasarkan tren.
* Dark web monitoring: memantau aktivitas di forum/marketplace tersembunyi.
* Integration with threat intelligence: menghubungkan OSINT dengan data internal organisasi.
* Customizable dashboards: sesuai kebutuhan instansi atau perusahaan.



Notes:

* Dibuat dengan blazor server, .NET 10, D3JS, dengan desain neo brutalism yang modern dengan dukungan dark theme/light
* Semua konfigurasi (ApiKey, ConnectionString, EndPoint, dsb) disimpan di file dan bisa diubah dari aplikasi
* Tambahkan readme.md (English dan Bahasa Indonesia)
* Database support SQLite, SQLServer, MySQL, Postgre
* Storage Support: FileSystem, AzureBlob, S3, MinIO
* Tambahkan dokumentasi lengkap di folder docs
* Buatkan banyak sample data dan user
* optimasi kode agar aplikasi cepat dan ringan
* REST API: Integrasi dengan aplikasi eksternal dengan Min API dan swagger

