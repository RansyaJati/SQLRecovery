namespace SQLRecovery.Export;

/// <summary>Creates exporters and provides batch export helpers.</summary>
public sealed class ExportService
{
    public IExporter GetExporter(ExportFormat format) => format switch
    {
        ExportFormat.Csv => new CsvExporter(),
        ExportFormat.SqlScript => new SqlScriptExporter(),
        ExportFormat.Json => new JsonExporter(),
        ExportFormat.Excel => new ExcelExporter(),
        ExportFormat.Sqlite => new SqliteExporter(),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    public static string GetDefaultExtension(ExportFormat format) => format switch
    {
        ExportFormat.Csv => ".csv",
        ExportFormat.SqlScript => ".sql",
        ExportFormat.Json => ".json",
        ExportFormat.Excel => ".xlsx",
        ExportFormat.Sqlite => ".db",
        _ => ".dat"
    };

    public static string GetFileFilter(ExportFormat format) => format switch
    {
        ExportFormat.Csv => "CSV files (*.csv)|*.csv",
        ExportFormat.SqlScript => "SQL scripts (*.sql)|*.sql",
        ExportFormat.Json => "JSON files (*.json)|*.json",
        ExportFormat.Excel => "Excel workbooks (*.xlsx)|*.xlsx",
        ExportFormat.Sqlite => "SQLite databases (*.db)|*.db",
        _ => "All files (*.*)|*.*"
    };

    /// <summary>Exports a single table to <paramref name="destinationPath"/>.</summary>
    public Task ExportTableAsync(
        ExportFormat format,
        Core.Schema.TableSchema table,
        IEnumerable<object?[]> rows,
        string destinationPath,
        ExportOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var exporter = GetExporter(format);
        return exporter.ExportAsync(table, rows, destinationPath, options, progress, cancellationToken);
    }
}
