using System.Globalization;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Export;

/// <summary>Formats CLR values as invariant text or as T-SQL literals.</summary>
public static class ValueFormatter
{
    public static string ToInvariantString(object? value) => value switch
    {
        null => string.Empty,
        string s => s,
        bool b => b ? "1" : "0",
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture),
        TimeSpan ts => ts.ToString(),
        byte[] bytes => "0x" + Convert.ToHexString(bytes),
        LobPointer lob => lob.ToString(),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };

    public static bool IsBinary(object? value) => value is byte[];

    /// <summary>
    /// Formats a value as a SQL literal for the specified dialect. When the column is known,
    /// non-Unicode string columns are emitted without the N prefix in T-SQL so the script
    /// keeps the original type.
    /// </summary>
    public static string ToSqlLiteral(object? value, ColumnDefinition? column = null, SqlDialect dialect = SqlDialect.SqlServer)
    {
        switch (value)
        {
            case null:
                return "NULL";
            case bool b:
                return dialect switch
                {
                    SqlDialect.PostgreSql => b ? "TRUE" : "FALSE",
                    _ => b ? "1" : "0"
                };
            case byte[] bytes:
                if (bytes.Length == 0)
                {
                    return dialect switch
                    {
                        SqlDialect.MySql => "X''",
                        SqlDialect.PostgreSql => "''::bytea",
                        _ => "0x"
                    };
                }
                return dialect switch
                {
                    SqlDialect.MySql => "X'" + Convert.ToHexString(bytes) + "'",
                    SqlDialect.PostgreSql => "'\\x" + Convert.ToHexString(bytes) + "'::bytea",
                    _ => "0x" + Convert.ToHexString(bytes)
                };
            case LobPointer:
                return "NULL";
            case DateTime dt:
                return dialect switch
                {
                    SqlDialect.MySql => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
                    SqlDialect.PostgreSql => "'" + dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture) + "'",
                    _ => "'" + dt.ToString("yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture) + "'"
                };
            case DateTimeOffset dto:
                return dialect switch
                {
                    SqlDialect.MySql => "'" + dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "'",
                    SqlDialect.PostgreSql => "'" + dto.ToString("yyyy-MM-dd HH:mm:ss.ffffffzzz", CultureInfo.InvariantCulture) + "'",
                    _ => "'" + dto.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture) + "'"
                };
            case TimeSpan ts:
                return dialect switch
                {
                    SqlDialect.MySql => "'" + ts.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) + "'",
                    SqlDialect.PostgreSql => "'" + ts.ToString(@"hh\:mm\:ss\.ffffff", CultureInfo.InvariantCulture) + "'",
                    _ => "'" + ts.ToString(@"hh\:mm\:ss\.fffffff", CultureInfo.InvariantCulture) + "'"
                };
            case Guid g:
                return "'" + g.ToString("D") + "'";
            case string s:
                return StringLiteral(s, column, dialect);
            case char c:
                return StringLiteral(c.ToString(), column, dialect);
            case float f:
                return f.ToString("R", CultureInfo.InvariantCulture);
            case double d:
                return d.ToString("R", CultureInfo.InvariantCulture);
            case IFormattable formattable:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            default:
                return StringLiteral(value.ToString()!, column, dialect);
        }
    }

    private static string StringLiteral(string value, ColumnDefinition? column, SqlDialect dialect)
    {
        if (dialect == SqlDialect.MySql)
        {
            // MySQL escapes with backslash
            string text = value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\0", "\\0")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
            return "'" + text + "'";
        }

        string standardEscaped = value.Replace("'", "''");

        if (dialect == SqlDialect.PostgreSql)
            return "'" + standardEscaped + "'";

        // SQL Server (T-SQL) N'...' vs '...'
        bool needsUnicodePrefix = column is null
            || column.Type is SqlDbType.NVarChar or SqlDbType.NChar or SqlDbType.NText
                or SqlDbType.Xml or SqlDbType.Unknown
            || !IsAscii(standardEscaped);

        return (needsUnicodePrefix ? "N'" : "'") + standardEscaped + "'";
    }

    private static bool IsAscii(string value)
    {
        foreach (char c in value)
        {
            if (c > 127)
                return false;
        }

        return true;
    }

    public static string QuoteIdentifier(string identifier, SqlDialect dialect = SqlDialect.SqlServer) => dialect switch
    {
        SqlDialect.SqlServer => "[" + identifier.Replace("]", "]]") + "]",
        SqlDialect.MySql => "`" + identifier.Replace("`", "``") + "`",
        SqlDialect.PostgreSql => "\"" + identifier.Replace("\"", "\"\"") + "\"",
        _ => "[" + identifier.Replace("]", "]]") + "]"
    };

    public static string QuoteQualified(TableSchema table, string? overrideName = null, SqlDialect dialect = SqlDialect.SqlServer)
    {
        if (!string.IsNullOrWhiteSpace(overrideName))
            return QuoteIdentifier(overrideName!, dialect);

        return dialect switch
        {
            SqlDialect.SqlServer => QuoteIdentifier(table.SchemaName, dialect) + "." + QuoteIdentifier(table.Name, dialect),
            SqlDialect.MySql => QuoteIdentifier(table.Name, dialect),
            SqlDialect.PostgreSql => QuoteIdentifier(table.SchemaName.ToLowerInvariant(), dialect) + "." + QuoteIdentifier(table.Name, dialect),
            _ => QuoteIdentifier(table.SchemaName, dialect) + "." + QuoteIdentifier(table.Name, dialect)
        };
    }

    public static string QuoteCsv(string value, ExportOptions options)
    {
        bool needsQuotes = options.QuoteAllFields
            || value.Contains(options.CsvQuote)
            || value.Contains(options.CsvDelimiter)
            || value.Contains('\n')
            || value.Contains('\r');

        if (!needsQuotes)
            return value;

        return options.CsvQuote + value.Replace(options.CsvQuote, options.CsvQuote + options.CsvQuote) + options.CsvQuote;
    }
}
