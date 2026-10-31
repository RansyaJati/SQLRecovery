using SQLRecovery.Core;
using SQLRecovery.Export;

if (args.Length == 0)
{
    Console.WriteLine("usage: Probe <mdf> [table] [--export <folder>]");
    return;
}

try
{
    using var db = DatabaseService.Open(args[0]);

    Console.WriteLine($"Database : {db.Info.Name}");
    Console.WriteLine($"Version  : {db.Info.VersionDisplay} ({db.Info.InternalVersion})");
    Console.WriteLine($"Pages    : {db.Info.TotalPages:N0}");
    Console.WriteLine($"Files    : {db.Info.FileCount}");
    Console.WriteLine($"Catalog  : available={db.Catalog.IsAvailable} error={db.Catalog.Error}");
    Console.WriteLine($"Objects  : {db.Catalog.Objects.Count}  Rowsets: {db.Catalog.Rowsets.Count}  Columns: {db.Catalog.Columns.Count}");
    Console.WriteLine($"Tables   : {db.Tables.Count}");
    Console.WriteLine();

    foreach (var t in db.Tables.Take(40))
    {
        Console.WriteLine($"{t.FullName,-40} type={t.ObjectType,-2} rows={t.RowCount,10:N0} cols={t.Columns.Count,3} " +
                          $"heap={t.IsHeap,-5} first={t.FirstPage} iam={t.FirstIamPage} root={t.RootPage}");
    }

    string? tableName = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;

    var target = tableName is not null
        ? db.Tables.FirstOrDefault(t => t.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
        : db.Tables.FirstOrDefault(t => t.IsUserTable && t.Columns.Count > 0);

    if (target is null)
    {
        Console.WriteLine("No target table found.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"--- {target.FullName} catalog detail (objectId={target.ObjectId}) ---");

    foreach (var r in db.Catalog.Rowsets.Where(r => r.IdMajor == target.ObjectId))
        Console.WriteLine($"  rowset id={r.RowsetId} major={r.IdMajor} minor={r.IdMinor} rows={r.RowCount:N0}");

    foreach (var i in target.Indexes)
        Console.WriteLine($"  index {i.IndexId} {i.Name} type={i.Type} rowset={i.Rowset}");

    Console.WriteLine();
    Console.WriteLine($"--- {target.FullName} columns ---");

    foreach (var c in target.Columns)
        Console.WriteLine($"  [{c.ColumnId}] {c.Name,-28} {c.TypeDisplayName,-24} nullable={c.IsNullable} identity={c.IsIdentity}");

    Console.WriteLine("--- first rows ---");

    int count = 0;
    int total = 0;

    foreach (var row in db.ReadRows(target))
    {
        total++;

        if (count < 12)
        {
            Console.WriteLine(string.Join(" | ", row.Select(Format)));
            count++;
        }

        if (total >= 2000)
            break;
    }

    Console.WriteLine($"--- scanned {total:N0} rows ---");

    int exportIndex = Array.IndexOf(args, "--export");

    if (exportIndex >= 0 && exportIndex + 1 < args.Length)
        await ExportAllFormatsAsync(db, target, args[exportIndex + 1]);
}
catch (Exception ex)
{
    Console.WriteLine("FAILED: " + ex);
}

static async Task ExportAllFormatsAsync(DatabaseService db, SQLRecovery.Core.Schema.TableSchema table, string folder)
{
    Directory.CreateDirectory(folder);

    var service = new ExportService();
    var options = new ExportOptions();

    Console.WriteLine();
    Console.WriteLine("--- exporting ---");

    foreach (var format in Enum.GetValues<ExportFormat>())
    {
        string extension = ExportService.GetDefaultExtension(format);
        string path = Path.Combine(folder, $"{table.SchemaName}.{table.Name}{extension}");

        try
        {
            await service.ExportTableAsync(format, table, db.ReadRows(table), path, options);
            var info = new FileInfo(path);
            Console.WriteLine($"  {format,-10} {info.Name,-40} {info.Length,12:N0} bytes");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {format,-10} FAILED: {ex.Message}");
        }
    }

    // Export MySQL dialect
    try
    {
        string mysqlPath = Path.Combine(folder, $"{table.SchemaName}.{table.Name}.mysql.sql");
        var mysqlOptions = new ExportOptions { SqlDialect = SqlDialect.MySql };
        await service.ExportTableAsync(ExportFormat.SqlScript, table, db.ReadRows(table), mysqlPath, mysqlOptions);
        var info = new FileInfo(mysqlPath);
        Console.WriteLine($"  {"MySQL",-10} {info.Name,-40} {info.Length,12:N0} bytes");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  MySQL      FAILED: {ex.Message}");
    }

    // Export PostgreSQL dialect
    try
    {
        string pgsqlPath = Path.Combine(folder, $"{table.SchemaName}.{table.Name}.pgsql.sql");
        var pgsqlOptions = new ExportOptions { SqlDialect = SqlDialect.PostgreSql };
        await service.ExportTableAsync(ExportFormat.SqlScript, table, db.ReadRows(table), pgsqlPath, pgsqlOptions);
        var info = new FileInfo(pgsqlPath);
        Console.WriteLine($"  {"Postgre",-10} {info.Name,-40} {info.Length,12:N0} bytes");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Postgre    FAILED: {ex.Message}");
    }

    Console.WriteLine();
    Console.WriteLine("--- verifying ---");

    VerifySqlite(Path.Combine(folder, $"{table.SchemaName}.{table.Name}.db"), table);
    VerifyExcel(Path.Combine(folder, $"{table.SchemaName}.{table.Name}.xlsx"), table);
}

static void VerifySqlite(string path, SQLRecovery.Core.Schema.TableSchema table)
{
    try
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Mode=ReadOnly");
        connection.Open();

        using var count = connection.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM {QuoteSqlite(table.Name)}";
        var rows = Convert.ToInt64(count.ExecuteScalar());

        using var peek = connection.CreateCommand();
        peek.CommandText = $"SELECT * FROM {QuoteSqlite(table.Name)} LIMIT 3";
        using var reader = peek.ExecuteReader();

        var columns = string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(reader.GetName));
        Console.WriteLine($"  sqlite     rows={rows:N0} (expected {table.RowCount:N0}) columns={columns}");

        while (reader.Read())
            Console.WriteLine("    " + string.Join(" | ", Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.IsDBNull(i) ? "NULL" : Format(reader.GetValue(i)))));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  sqlite     VERIFY FAILED: {ex.Message}");
    }
}

static void VerifyExcel(string path, SQLRecovery.Core.Schema.TableSchema table)
{
    try
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook(path);
        var sheet = workbook.Worksheets.First();
        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;

        Console.WriteLine($"  excel      sheet='{sheet.Name}' rows={lastRow - 1:N0} (expected {table.RowCount:N0}) " +
                          $"columns={sheet.LastColumnUsed()?.ColumnNumber() ?? 0}");

        for (int row = 2; row <= Math.Min(4, lastRow); row++)
        {
            int lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var rowValues = new List<string>();

            for (int c = 1; c <= lastCol; c++)
            {
                var cell = sheet.Cell(row, c);
                object? val = cell.Value;
                rowValues.Add(Format(val));
            }

            Console.WriteLine("    " + string.Join(" | ", rowValues));
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  excel      VERIFY FAILED: {ex.Message}");
    }
}

static string QuoteSqlite(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

static string Format(object? value) => value switch
{
    null => "NULL",
    byte[] bytes => "0x" + Convert.ToHexString(bytes),
    DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
    string s when s.Length > 80 => s[..80] + "…",
    _ => value.ToString() ?? "NULL"
};
