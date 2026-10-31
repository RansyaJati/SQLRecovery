# SQL-Saver

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C# 12](https://img.shields.io/badge/C%23-12.0-brightgreen.svg)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![WPF-UI](https://img.shields.io/badge/GUI-WPF--UI%20(Fluent)-purple.svg)](https://github.com/lepoco/wpfui)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-0078D6.svg)](https://microsoft.com/windows)

**SQL-Saver** is a high-performance Windows desktop application designed to inspect, preview, recover, and extract table data directly from Microsoft SQL Server physical database files (**`.mdf`**, **`.ndf`**, **`.ldf`**) without requiring an active SQL Server instance or database attachment.

Whether your SQL Server instance is corrupted, inaccessible, or you need to recover legacy databases from decades ago, SQL-Saver reads raw 8KB database pages from disk, reconstructs the schema catalog, decodes slot arrays, traverses allocation maps, and exports clean datasets to modern database engines and analytics formats.

---

## 🌟 Key Features

### 1. Direct Binary Storage Engine
* **No SQL Server Instance Needed:** Directly parses raw binary pages (`PageHeader`, `SlotArray`, `PrimaryRecord`, `GhostRecords`, `BlobPointers`).
* **Broad Version Support:** Seamlessly reads databases from **SQL Server 2000 (Internal Version 539)** up to **SQL Server 2022 (Internal Version 998)**.
* **SQL Server 2000 Legacy Bootstrap:** Automatically falls back to legacy system catalogs (`sysobjects`, `sysindexes`, `syscolumns`, IAM single-page allocation slots) for ancient databases.
* **Clustered & Heap Traversal:** Navigates both B-Tree index leaf chains and Heap IAM extent maps.
* **Full LOB Reconstruction:** Automatically dereferences and reassembles fragmented off-row Large Objects (Text, Image, `VARCHAR(MAX)`, `NVARCHAR(MAX)`, `VARBINARY(MAX)`).

### 2. Multi-Format & Multi-Dialect Export
* **Multi-Dialect SQL Scripts:**
  * **SQL Server (T-SQL):** Square-bracket identifiers (`[Table]`), Unicode `N'...'` literals, and `GO` batch separators.
  * **MySQL / MariaDB:** Backtick quotation (`` `Table` ``), MySQL-compliant escaping (`\'`, `\\`), `ENGINE=InnoDB DEFAULT CHARSET=utf8mb4`, and foreign key check suppression during batch insert.
  * **PostgreSQL:** Double-quote identifiers (`"dbo"."Table"`), boolean literals (`TRUE`/`FALSE`), bytea hex literals (`'\x...'::bytea`), and standard schema quoting.
* **Excel Workbook (`.xlsx`):** Formatted spreadsheets with auto-filters, frozen header panes, and auto-splitting across sheets for massive rowsets.
* **SQLite Database (`.db`):** Standalone SQLite single-file database creation with native type mapping and transaction batching.
* **CSV (`.csv`):** Configurable delimiters (comma, semicolon, tab, pipe), RFC 4180 quoting, and UTF-8 BOM encoding.
* **JSON (`.json`):** Standard formatted array of row objects or Newline-Delimited JSON (NDJSON) for streaming pipelines.

### 3. Modern Fluent UI
* Built with **WPF-UI 3.0.5** adhering to the Windows 11 Fluent Design System.
* Dark mode (default) and Light mode surfaces.
* Instant table search and schema inspection.
* High-speed virtualized DataGrid with pagination (200 rows per page) capable of browsing tables with hundreds of thousands of rows smoothly.
* Convenient shortcuts (`Ctrl+O` to open, `Ctrl+E` to export, `F5` to refresh).
* Recent files history stored safely in AppData.

---

## 🏗️ Architecture

```
SQL-Saver
├── src/
│   ├── SQLRecovery.Core/       # Core binary engine for MDF/NDF/LDF
│   │   ├── Allocation/         # GAM, SGAM, IAM, PFS allocation maps
│   │   ├── Catalog/            # System catalog reader (2000 & 2005-2022)
│   │   ├── Engine/             # Page reader, slot array, record decoder, LOB reassembler
│   │   ├── Schema/             # TableSchema, ColumnDefinition, IndexDefinition
│   │   ├── SqlTypes/           # Temporal, numeric, decimal, string, and binary decoders
│   │   └── DatabaseService.cs  # High-level database access API
│   │
│   ├── SQLRecovery.Export/     # Extensible multi-format exporter pipeline
│   │   ├── CsvExporter.cs
│   │   ├── ExcelExporter.cs    (ClosedXML)
│   │   ├── JsonExporter.cs
│   │   ├── SqliteExporter.cs   (Microsoft.Data.Sqlite)
│   │   ├── SqlScriptExporter.cs (Multi-dialect T-SQL / MySQL / PostgreSQL)
│   │   ├── SqlTypeMapper.cs
│   │   └── ValueFormatter.cs
│   │
│   └── SQLRecovery.App/        # WPF Modern Desktop Application (MVVM)
│       ├── Services/           # Dialog, settings, recent files service
│       ├── ViewModels/         # MainViewModel, RowItem, TableListItem
│       └── Views/              # MainWindow, ExportWindow, Styles & Themes
│
├── tools/
│   └── Probe/                  # Diagnostic CLI utility for automated page verification
│
└── tests/
    └── SQLRecovery.Tests/      # Automated xUnit regression test suite
```

---

## 🚀 Getting Started

### Prerequisites
* Windows 10 (Version 1903+) or Windows 11
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/RansyaJati/SQL-Saver.git
   cd SQL-Saver
   ```

2. Build the solution:
   ```bash
   dotnet build SQLRecovery/SQLRecovery.slnx
   ```

3. Run the automated tests:
   ```bash
   dotnet test SQLRecovery/tests/SQLRecovery.Tests/SQLRecovery.Tests.csproj
   ```

4. Launch the application:
   ```bash
   dotnet run --project SQLRecovery/src/SQLRecovery.App/SQLRecovery.App.csproj
   ```

### Publishing Standalone Executable
To publish a self-contained or framework-dependent release binary:
```bash
dotnet publish SQLRecovery/src/SQLRecovery.App/SQLRecovery.App.csproj -c Release -o ./publish
```
The runnable executable will be located in `./publish/SQLRecovery.App.exe`.

---

## 🧪 Real-World Database Verification

SQL-Saver has been verified against both synthetic test databases and production legacy databases:
* **SQL Server 2022 (`SqlRecTest.mdf` - Internal Version 998):**
  * Tables spanning all standard data types (`datetime2`, `decimal(18,4)`, `nvarchar(max)`, `uniqueidentifier`, `money`, `bit`).
  * 100% data fidelity verified across CSV, JSON, SQLite, Excel, and SQL scripts.
* **SQL Server 2000 (`dbLatihan.mdf` - Internal Version 539):**
  * 85 user and system tables successfully cataloged.
  * Extracted large clustered tables (e.g., `dtbil` with **240,571 rows**) via leaf page chaining.
  * Decoded heap tables (`harga_detail`, `Diskon_Detail`) via single-page allocation slot IAM traversal.

---

## 📄 License & Attribution

This project is licensed under the [MIT License](LICENSE) - see the [LICENSE](LICENSE) file for details.

This project is built on top of excellent open-source libraries. Please review [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for full notices and license texts of third-party components (including WPF-UI, ClosedXML, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, and Serilog).

---

## 🤝 Contributing

Contributions, bug reports, and feature suggestions are welcome! Feel free to open an issue or submit a pull request on GitHub.
