using System.Text;
using SQLRecovery.Core.Schema;

namespace SQLRecovery.Export;

public sealed class CsvExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Csv;
    public string FileExtension => ".csv";
    public string DisplayName => "CSV (comma separated values)";

    public async Task ExportAsync(
        TableSchema table,
        IEnumerable<object?[]> rows,
        string destinationPath,
        ExportOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream, options.Encoding) { NewLine = options.NewLine };

        if (options.IncludeHeaders)
        {
            var header = string.Join(options.CsvDelimiter,
                table.Columns.Select(c => ValueFormatter.QuoteCsv(c.Name, options)));
            await writer.WriteLineAsync(header);
        }

        long count = 0;
        long expected = table.RowCount;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var builder = new StringBuilder();
            for (int i = 0; i < table.Columns.Count; i++)
            {
                if (i > 0)
                    builder.Append(options.CsvDelimiter);

                string text = i < row.Length ? ValueFormatter.ToInvariantString(row[i]) : string.Empty;
                builder.Append(ValueFormatter.QuoteCsv(text, options));
            }

            await writer.WriteLineAsync(builder.ToString());

            count++;

            if (expected > 0 && count % 2000 == 0)
                progress?.Report(Math.Min(1.0, (double)count / expected));
        }

        await writer.FlushAsync(cancellationToken);
        progress?.Report(1.0);
    }
}
