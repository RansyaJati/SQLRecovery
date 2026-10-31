using Microsoft.Data.Sqlite;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Export;

public sealed class SqliteExporter : IExporter
{
    public ExportFormat Format => ExportFormat.Sqlite;
    public string FileExtension => ".db";
    public string DisplayName => "SQLite database";

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

        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        var connectionString = new SqliteConnectionStringBuilder { DataSource = destinationPath }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        string tableName = string.IsNullOrWhiteSpace(options.TableNameOverride) ? table.Name : options.TableNameOverride!;
        string quotedTable = Quote(tableName);

        var columns = table.Columns
            .Select((column, index) => (Column: column, Index: index))
            .Where(x => !x.Column.IsComputed)
            .ToList();

        var createSql = "CREATE TABLE " + quotedTable + " (" +
                        string.Join(", ", columns.Select(x => Quote(x.Column.Name) + " " + MapType(x.Column))) +
                        ");";

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = createSql;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var insertSql = "INSERT INTO " + quotedTable + " (" +
                        string.Join(", ", columns.Select(x => Quote(x.Column.Name))) + ") VALUES (" +
                        string.Join(", ", columns.Select((_, i) => "$p" + i)) + ");";

        await using var insert = connection.CreateCommand();
        insert.CommandText = insertSql;
        insert.Transaction = (SqliteTransaction)transaction;

        var parameters = new SqliteParameter[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            parameters[i] = insert.CreateParameter();
            parameters[i].ParameterName = "$p" + i;
            insert.Parameters.Add(parameters[i]);
        }

        long count = 0;
        long expected = table.RowCount;

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (int i = 0; i < columns.Count; i++)
            {
                var value = columns[i].Index < row.Length ? row[columns[i].Index] : null;
                parameters[i].Value = ToSqliteValue(value);
            }

            await insert.ExecuteNonQueryAsync(cancellationToken);
            count++;

            if (expected > 0 && count % 5000 == 0)
                progress?.Report(Math.Min(1.0, (double)count / expected));
        }

        await transaction.CommitAsync(cancellationToken);
        progress?.Report(1.0);
    }

    private static string MapType(ColumnDefinition column) => column.Type switch
    {
        SqlDbType.TinyInt or SqlDbType.SmallInt or SqlDbType.Int or SqlDbType.BigInt or SqlDbType.Bit => "INTEGER",
        SqlDbType.Real or SqlDbType.Float => "REAL",
        SqlDbType.Binary or SqlDbType.VarBinary or SqlDbType.Image or SqlDbType.Timestamp => "BLOB",
        _ => "TEXT"
    };

    private static object ToSqliteValue(object? value) => value switch
    {
        null => DBNull.Value,
        bool b => b ? 1L : 0L,
        byte[] bytes => bytes,
        LobPointer lob => lob.ToString(),
        DateTime dt => dt.ToString("O"),
        DateTimeOffset dto => dto.ToString("O"),
        TimeSpan ts => ts.ToString(),
        Guid g => g.ToString("D"),
        decimal m => m.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value
    };

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
