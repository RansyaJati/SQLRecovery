using SQLRecovery.Core.Schema;

namespace SQLRecovery.Export;

/// <summary>Writes table rows to an output format.</summary>
public interface IExporter
{
    ExportFormat Format { get; }
    string FileExtension { get; }
    string DisplayName { get; }

    Task ExportAsync(
        TableSchema table,
        IEnumerable<object?[]> rows,
        string destinationPath,
        ExportOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
