using ClosedXML.Excel;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Export;

public sealed class ExcelExporter : IExporter
{
    private const int MaxRowsPerSheet = 1_000_000;

    public ExportFormat Format => ExportFormat.Excel;
    public string FileExtension => ".xlsx";
    public string DisplayName => "Excel workbook (.xlsx)";

    public Task ExportAsync(
        TableSchema table,
        IEnumerable<object?[]> rows,
        string destinationPath,
        ExportOptions options,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Export(table, rows, destinationPath, options, progress, cancellationToken), cancellationToken);
    }

    private static void Export(
        TableSchema table,
        IEnumerable<object?[]> rows,
        string destinationPath,
        ExportOptions options,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        using var workbook = new XLWorkbook();

        string baseName = SanitizeSheetName(string.IsNullOrWhiteSpace(options.TableNameOverride)
            ? table.Name
            : options.TableNameOverride!);

        IXLWorksheet worksheet = workbook.AddWorksheet(baseName);
        int sheetIndex = 1;
        int rowIndex = 1;

        WriteHeader(worksheet, table, options);

        long count = 0;
        long expected = table.RowCount;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            rowIndex++;

            if (rowIndex > MaxRowsPerSheet)
            {
                worksheet = workbook.AddWorksheet($"{baseName}_{++sheetIndex}");
                rowIndex = 1;
                WriteHeader(worksheet, table, options);
                rowIndex = 2;
            }

            for (int c = 0; c < table.Columns.Count; c++)
            {
                var value = c < row.Length ? row[c] : null;
                SetCell(worksheet.Cell(rowIndex, c + 1), value);
            }

            count++;

            if (expected > 0 && count % 5000 == 0)
                progress?.Report(Math.Min(1.0, (double)count / expected));
        }

        foreach (var ws in workbook.Worksheets)
        {
            ws.SheetView.FreezeRows(1);
            ws.Columns().AdjustToContents(1, Math.Min(200, Math.Max(1, ws.LastRowUsed()?.RowNumber() ?? 1)));
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 8);
        }

        workbook.SaveAs(destinationPath);
        progress?.Report(1.0);
    }

    private static void WriteHeader(IXLWorksheet worksheet, TableSchema table, ExportOptions options)
    {
        if (!options.IncludeHeaders)
            return;

        for (int c = 0; c < table.Columns.Count; c++)
        {
            var cell = worksheet.Cell(1, c + 1);
            cell.Value = table.Columns[c].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D2D44");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        worksheet.Row(1).Height = 20;
        worksheet.Range(1, 1, 1, Math.Max(1, table.Columns.Count)).SetAutoFilter();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                cell.Clear(XLClearOptions.Contents);
                break;
            case bool b:
                cell.Value = b;
                break;
            case byte n:
                cell.Value = n;
                break;
            case short n:
                cell.Value = n;
                break;
            case int n:
                cell.Value = n;
                break;
            case long n:
                cell.Value = n;
                break;
            case float f:
                cell.Value = f;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal m:
                cell.Value = m;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            case TimeSpan ts:
                cell.Value = ts.ToString();
                break;
            case byte[] bytes:
                cell.Value = "0x" + Convert.ToHexString(bytes);
                break;
            case LobPointer lob:
                cell.Value = lob.ToString();
                break;
            case Guid g:
                cell.Value = g.ToString();
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static string SanitizeSheetName(string name)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());

        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "Sheet";

        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }
}
