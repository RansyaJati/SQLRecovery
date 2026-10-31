using System.Text;

namespace SQLRecovery.Export;

public sealed class ExportOptions
{
    public Encoding Encoding { get; set; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
    public string CsvDelimiter { get; set; } = ",";
    public string CsvQuote { get; set; } = "\"";
    public bool QuoteAllFields { get; set; }
    public bool IncludeHeaders { get; set; } = true;
    public bool IncludeCreateTable { get; set; } = true;
    public bool IncludeIndexes { get; set; } = true;
    public bool JsonLines { get; set; }
    public bool WriteIndented { get; set; } = true;
    public int InsertBatchSize { get; set; } = 1000;
    public SqlDialect SqlDialect { get; set; } = SqlDialect.SqlServer;
    public string NewLine { get; set; } = "\r\n";
    public string? TableNameOverride { get; set; }
    public string? DatabaseName { get; set; }
}
