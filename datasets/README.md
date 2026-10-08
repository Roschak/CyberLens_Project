# Datasets

Kumpulan dataset referensi untuk CyberLens (OSINT & media monitoring).

## `klasifikasi_khusus_jakarta/`

Klasifikasi khusus anggota parlemen wilayah DKI Jakarta 2024–2029
(Fraksi PKS, Golkar, PDI Perjuangan):

- **DPR RI Dapil DKI Jakarta I/II/III** — 11 tokoh (`dpr_ri_dki_jakarta/`)
- **DPRD Provinsi DKI Jakarta Dapil 1–10** — 44 tokoh (`dprd_provinsi_dki_jakarta/`)
- Rekap CSV: `rekapitulasi_khusus_jakarta.csv`
- Rekap sosmed & tagar: `rekapitulasi_sosmed_tagar_jakarta.csv`, `sosmed_tagar_jakarta.md`

Dokumentasi lengkap: lihat `klasifikasi_khusus_jakarta/README_JAKARTA.md`.

### Cara pakai di CyberLens

1. **Watch Keywords** — impor nama tokoh sebagai keyword pantauan
   (Settings → Watch Keywords), mis. dari kolom `nama` di CSV.
2. **Entity Network** — nama tokoh / partai / dapil sebagai `EntityNode`
   (Person / Organization / Location).
3. **GeoMap / Globe** — gunakan kolom `dapil` / wilayah Jakarta untuk geocoding
   (`SimpleGeocoder`) dan visualisasi spasial.
4. **AI Analytics / Bang Kevin** — jadikan profil `.md` sebagai konteks
   pengetahuan untuk briefing intelijen.

Sumber data: `https://github.com/Iruzzz67/data-tokoh-partai`
