# Rencana Kerja: SQL Recovery Tool (MDF/NDF/LDF Extractor)

Dokumen ini memuat arsitektur, progres implementasi, hasil verifikasi, serta langkah kerja kelanjutan dari aplikasi desktop pemulihan data file basis data SQL Server (.mdf, .ndf, .ldf) ke berbagai format output modern.

---

## 1. Ringkasan Eksekutif & Status Proyek

* **Status Saat Ini:** Fase Implementasi & Validasi Penuh Selesai (100%).
* **Platform:** Windows Desktop (.NET 8 + WPF, C# 12).
* **Desain UI:** Fluent Design System Modern (WPF-UI 3.0.5) dengan tema Gelap (default) & Terang.
* **Format Input Didukung:** MDF (Primary Data File), NDF (Secondary Data File), LDF (Transaction Log File).
* **Versi SQL Server Teruji:** SQL Server 2000 (Internal Version 539) hingga SQL Server 2022 (Internal Version 998). Dukungan penuh katalog sistem SQL Server 2000 (sysobjects, sysindexes, syscolumns, IAM allocation).
* **Format Output Didukung:** CSV, SQL Script multi-dialek (SQL Server T-SQL, MySQL / MariaDB, PostgreSQL), JSON (Format Standar & NDJSON), Excel (.xlsx via ClosedXML), dan SQLite (.db via Microsoft.Data.Sqlite).

---

## 2. Arsitektur Solusi & Komponen

Solusi dibangun dengan modularitas tinggi, memisahkan parsing format biner database dari abstraksi ekspor dan presentasi visual:

```
D:\Project Penting\App SQLRecovery\SQLRecovery\
│
├── SQLRecovery.slnx                    (Solution File)
│
├── src\
│   ├── SQLRecovery.Core\               (Pustaka Parsing Biner MDF/NDF/LDF)
│   │   ├── Engine\                     (Page Reader, Slot Array, PrimaryRecord, LobReader, BootPage)
│   │   ├── Allocation\                 (GAM, SGAM, IAM, PFS Extent Map)
│   │   ├── Catalog\                    (sysrowsets, sysallocunits, sysschobjs, syscolpars, TableDataReader)
│   │   ├── SqlTypes\                   (Tipe SQL: Temporal, Numerik, Desimal, String/Unicode, BLOB/LOB)
│   │   ├── Schema\                     (TableSchema, ColumnDefinition, IndexDefinition, DatabaseInfo)
│   │   └── DatabaseService.cs          (Facade API Utama)
│   │
│   ├── SQLRecovery.Export\             (Mesin Ekspor Data Multi-Format)
│   │   ├── CsvExporter.cs              (CSV streaming dengan konfigurasi delimiter, quote, BOM)
│   │   ├── SqlDialect.cs               (Enum target engine: SqlServer, MySql, PostgreSql)
│   │   ├── SqlTypeMapper.cs            (Mapping tipe data SQL Server ke sintaks DDL MySQL & PostgreSQL)
│   │   ├── SqlScriptExporter.cs        (CREATE TABLE + INSERT multi-dialek dengan batching & escaping spesifik)
│   │   ├── JsonExporter.cs             (JSON Array & JSON Lines streaming)
│   │   ├── ExcelExporter.cs            (ClosedXML .xlsx auto-filter, auto-fit, multi-sheet per 1M baris)
│   │   ├── SqliteExporter.cs           (Microsoft.Data.Sqlite mapping tipe native & batch transaction)
│   │   ├── ValueFormatter.cs           (Literals SQL multi-dialek, CSV escaper, Invariant representation)
│   │   └── ExportService.cs            (Dispatcher & Export Orchestration)
│   │
│   └── SQLRecovery.App\                (Aplikasi Desktop WPF Modern)
│       ├── App.xaml / App.xaml.cs      (Entry Point, DI Container, Global Crash Handling)
│       ├── MainWindow.xaml / .cs       (Jendela Utama, Sidebar Navigasi, Preview DataGrid, Dynamic Columns)
│       ├── ViewModels\
│       │   ├── MainViewModel.cs        (MVVM state, Paging 200 baris, File Watcher, Search filter)
│       │   └── RowItem.cs / TableListItem.cs
│       ├── Views\
│       │   └── ExportWindow.xaml / .cs (Modal Konfigurasi Ekspor Tabel / Seluruh Database)
│       └── Services\                   (DialogService, SettingsService recent files di AppData)
│
├── tools\
│   └── Probe\                          (Alat Diagnostik CLI & Validator Biner Database)
│
└── _testdb\                            (Basis Data Uji Nyata: SqlRecTest.mdf)
```

---

## 3. Rincian Masalah Teknis yang Telah Terselesaikan

1. **Mapping SysAllocUnits vs SysRowsets:**
   * *Problem:* SQL Server 2016-2022 tidak menghubungkan `sysrowsets.rowsetid` langsung dengan `sysallocunits.auid`.
   * *Solusi:* Memetakan `sysrowsets.rowsetid` ke kolom `sysallocunits.ownerid` (partition container id).
2. **Decoding Temporal Little-Endian:**
   * *Problem:* Tipe `datetime2(n)` dan `time(n)` membalikkan urutan byte waktu jika dibaca secara Big-Endian standar.
   * *Solusi:* Decoder diperbaiki membaca waktu dalam Little-Endian byte-by-byte (`TemporalTypes.cs`), menghasilkan nilai waktu akurat.
3. **Penyelarasan Nilai Decimal 9-Byte:**
   * *Problem:* Kolom `decimal(12,2)` dialokasikan 9 byte storage alih-alih 5 byte (presisi 10-19 butuh 9 byte), menggeser offset kolom berikutnya.
   * *Solusi:* Standar ukuran binary storage SQL Server disesuaikan pada kalkulasi offset kolom tetap.
4. **Ekstraksi LOB Penuh (Text / Image / NVARCHAR(MAX)):**
   * *Problem:* Kolom teks panjang hanya menyimpan 16-byte pointer di baris data (`LobPointer`).
   * *Solusi:* Implementasi `LobReader.cs` dan `TextMixPage.cs` untuk menelusuri struktur pointer rekursif LOB (`SMALL_ROOT`, `DATA`, `INTERNAL`, `LARGE_ROOT_YUKON`) dan merakit kembali string/byte aslinya secara utuh.
5. **Deteksi Boot Record Warisan (SQL 2000):**
   * *Problem:* File SQL Server 2000 (`dbLatihan.mdf`) meletakkan boot record pada offset statis halaman 9 (Page 9), bukan di slot array belakang.
   * *Solusi:* `BootPage.cs` dilengkapi mekanisme fallback membaca offset statis [96, 100, 104], mampu membaca nama database & versi internal tanpa crash.

---

## 4. Hasil Validasi Aktual (Pengujian Nyata)

### Database Uji: `SqlRecTest.mdf` (SQL Server 2022 - Internal Version 998)
* **Tabel `dbo.Customers` (250 baris):**
  * Tipe data: `int`, `nvarchar(120)`, `varchar(200)`, `nvarchar(30)`, `datetime2(3)`, `decimal(12,2)`, `bit`, `uniqueidentifier`, `nvarchar(max)`.
  * Hasil: 250 baris terbaca sempurna. Seluruh 5 format ekspor (CSV 33KB, SQL 40KB, JSON 81KB, XLSX 26KB, SQLite 45KB) berhasil dibuat dan divalidasi keakuratan datanya.
* **Tabel `dbo.OrderLines` (500 baris):**
  * Tipe data: `bigint`, `int`, `char(12)`, `smallint`, `money`, `date`, `real`.
  * Hasil: 500 baris terbaca dan berhasil diekspor ke semua format.
* **Tabel `dbo.AuditLog` (80 baris):**
  * Struktur: Heap IAM (tanpa clustered index), tipe `datetime`, `varchar`, `varbinary`.
  * Hasil: 80 baris terbaca dan diekspor lengkap.

### Database Warisan: `dbLatihan.mdf` (SQL Server 2000 - Internal Version 539)
* **Hasil Bootstrap Katalog:**
  * Terdeteksi: `Version: SQL Server 2000 (539), Pages: 13,688, Objects: 239, Rowsets: 226, Columns: 985, Tables: 85`.
  * Bootstrapping `sysindexes` dari `FirstSysIndexes` (Page 1:24), `sysobjects` dari Page 1:8, dan `syscolumns` dari Page 1:16.
  * Sanitasi nama tabel dan kolom dari quirk encoding karakter bit-tinggi SQL Server 2000 (`gol_harga`, `tbl_gol`, `mutasi_header`, dll).
* **Hasil Pembacaan Data Tabel:**
  * **Tabel `dbo.Supplier`:** 31 baris terbaca lengkap dengan 16 kolom (kode_supl, nama, alamat, kota, cara_byr, aktif, dll).
  * **Tabel `dbo.dtbil`:** 240.571 baris pada indeks terklaster dibaca berurutan melalui leaf page chain tanpa error.
  * **Tabel Heap (`harga_detail`, `koreksi_Detail`, `tbl_bank`):** Traversal IAM (Index Allocation Map) 8 single-page slots + mixed/uniform extent mapping berhasil membaca ribuan record aktif.

---

## 5. Rencana Kerja Selanjutnya & Status

| No | Modul / Komponen | Target Pekerjaan | Status |
|:---|:---|:---|:---|
| 1 | **Unit & Integration Test Suite** | Membuat proyek pengujian otomatis `tests/SQLRecovery.Tests` menggunakan xUnit (11 unit test passing). | **Selesai (Passed)** |
| 2 | **SQL Server 2000 Catalog & Heap Reader** | Bootstrap katalog sistem legacy dan traversal IAM slot/extent bitmap. | **Selesai (Verified)** |
| 3 | **Multi-Dialect SQL Exporter** | Menambahkan dukungan ekspor skrip SQL untuk SQL Server (T-SQL), MySQL / MariaDB (backticks, standard escaping, disabling FK checks), dan PostgreSQL (double quotes, boolean literal TRUE/FALSE, bytea literal, lowercase schema). | **Selesai (Verified)** |
| 4 | **WPF App Polish & User Guide** | Memastikan shortcut UI (Ctrl+O, Ctrl+E), status bar progress binding, dropdown dialek SQL pada ExportWindow, serta recent files di AppData. | **Selesai** |
| 5 | **Build & Packaging Distribution** | Build rilis framework-dependent di folder `publish/SQLRecovery.App.exe` terverifikasi dapat dijalankan langsung. | **Selesai** |
| 6 | **Corrupted Page Resilience (Heuristic Scan)** | Menambahkan opsi pemindaian heuristik untuk halaman yatim (unallocated/orphaned pages) jika diperlukan. | **Opsional / Lanjutan** |

---

## 6. Panduan Menjalankan Proyek

### Kompilasi Proyek:
```powershell
cd "D:\Project Penting\App SQLRecovery\SQLRecovery"
dotnet build SQLRecovery.slnx
```

### Menjalankan UI Desktop:
```powershell
dotnet run --project src\SQLRecovery.App\SQLRecovery.App.csproj
```

### Menjalankan Diagnostik CLI (Probe Tool):
```powershell
# Memeriksa tabel dan mengekspor hasilnya
dotnet run --project tools\Probe\Probe.csproj -- "D:\Project Penting\App SQLRecovery\_testdb\SqlRecTest.mdf" Customers --export "$env:TEMP\sqlrec-export"
```
