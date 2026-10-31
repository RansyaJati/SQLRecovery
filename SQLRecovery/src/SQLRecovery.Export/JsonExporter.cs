using System.Text.Json;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Export;

public sealed class JsonExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Json;
    public string FileExtension => ".json";
    public string DisplayName => "JSON";

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

        var writerOptions = new JsonWriterOptions
        {
            Indented = options.WriteIndented && !options.JsonLines
        };

        await using var writer = new Utf8JsonWriter(stream, writerOptions);

        if (!options.JsonLines)
            writer.WriteStartArray();

        long count = 0;
        long expected = table.RowCount;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            writer.WriteStartObject();

            for (int i = 0; i < table.Columns.Count; i++)
            {
                writer.WritePropertyName(table.Columns[i].Name);

                if (i >= row.Length)
                {
                    writer.WriteNullValue();
                    continue;
                }

                WriteValue(writer, row[i]);
            }

            writer.WriteEndObject();
            writer.Flush();

            if (options.JsonLines)
            {
                stream.WriteByte((byte)'\n');
                writer.Reset();
            }

            count++;

            if (expected > 0 && count % 2000 == 0)
                progress?.Report(Math.Min(1.0, (double)count / expected));
        }

        if (!options.JsonLines)
        {
            writer.WriteEndArray();
            writer.Flush();
        }

        progress?.Report(1.0);
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case byte n:
                writer.WriteNumberValue(n);
                break;
            case short n:
                writer.WriteNumberValue(n);
                break;
            case int n:
                writer.WriteNumberValue(n);
                break;
            case long n:
                writer.WriteNumberValue(n);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("O"));
                break;
            case TimeSpan ts:
                writer.WriteStringValue(ts.ToString());
                break;
            case Guid g:
                writer.WriteStringValue(g);
                break;
            case byte[] bytes:
                writer.WriteStringValue("0x" + Convert.ToHexString(bytes));
                break;
            case LobPointer lob:
                writer.WriteStringValue(lob.ToString());
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
