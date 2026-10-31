using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;
using SQLRecovery.Export;
using Xunit;

namespace SQLRecovery.Tests;

public class ExporterPipelineTests
{
    private static TableSchema CreateSampleTable()
    {
        return new TableSchema
        {
            ObjectId = 1001,
            SchemaName = "dbo",
            Name = "TestProducts",
            RowCount = 2,
            DataIndexId = 1,
            Columns =
            [
                new ColumnDefinition { ColumnId = 1, Name = "Id", Type = SqlDbType.Int, IsNullable = false },
                new ColumnDefinition { ColumnId = 2, Name = "Name", Type = SqlDbType.NVarChar, MaxLength = 100, IsNullable = false },
                new ColumnDefinition { ColumnId = 3, Name = "Price", Type = SqlDbType.Decimal, Precision = 10, Scale = 2, IsNullable = false },
                new ColumnDefinition { ColumnId = 4, Name = "IsAvailable", Type = SqlDbType.Bit, IsNullable = false }
            ],
            Indexes = Array.Empty<IndexDefinition>()
        };
    }

    private static List<object?[]> CreateSampleRows() =>
    [
        [1, "Product Alpha", 150.75m, true],
        [2, "Product Beta", 299.00m, false]
    ];

    [Fact]
    public async Task CsvExporter_GeneratesValidCsv()
    {
        var exporter = new CsvExporter();
        var table = CreateSampleTable();
        var rows = CreateSampleRows();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.csv");

        try
        {
            await exporter.ExportAsync(table, rows, tempPath, new ExportOptions());
            Assert.True(File.Exists(tempPath));

            string[] lines = await File.ReadAllLinesAsync(tempPath);
            Assert.Equal(3, lines.Length);
            Assert.Equal("Id,Name,Price,IsAvailable", lines[0]);
            Assert.Equal("1,Product Alpha,150.75,1", lines[1]);
            Assert.Equal("2,Product Beta,299.00,0", lines[2]);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task JsonExporter_GeneratesValidJson()
    {
        var exporter = new JsonExporter();
        var table = CreateSampleTable();
        var rows = CreateSampleRows();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.json");

        try
        {
            await exporter.ExportAsync(table, rows, tempPath, new ExportOptions());
            Assert.True(File.Exists(tempPath));

            string json = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("\"Id\": 1", json);
            Assert.Contains("\"Name\": \"Product Alpha\"", json);
            Assert.Contains("\"Price\": 150.75", json);
            Assert.Contains("\"IsAvailable\": true", json);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SqliteExporter_ExportsAndReadsBack()
    {
        var exporter = new SqliteExporter();
        var table = CreateSampleTable();
        var rows = CreateSampleRows();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.db");

        try
        {
            await exporter.ExportAsync(table, rows, tempPath, new ExportOptions());
            Assert.True(File.Exists(tempPath));

            using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={tempPath};Mode=ReadOnly");
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"TestProducts\"";
            long count = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.Equal(2, count);

            conn.Close();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SqlScriptExporter_ExportsMySqlDialectCorrectly()
    {
        var exporter = new SqlScriptExporter();
        var table = CreateSampleTable();
        var rows = CreateSampleRows();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_mysql_{Guid.NewGuid():N}.sql");

        try
        {
            var options = new ExportOptions { SqlDialect = SqlDialect.MySql };
            await exporter.ExportAsync(table, rows, tempPath, options);
            Assert.True(File.Exists(tempPath));

            string sql = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("CREATE TABLE IF NOT EXISTS `TestProducts`", sql);
            Assert.Contains("`Id` INT NOT NULL", sql);
            Assert.Contains("`Name` VARCHAR(100) NOT NULL", sql);
            Assert.Contains("`Price` DECIMAL(10,2) NOT NULL", sql);
            Assert.Contains("`IsAvailable` TINYINT(1) NOT NULL", sql);
            Assert.Contains("ENGINE=InnoDB DEFAULT CHARSET=utf8mb4", sql);
            Assert.Contains("INSERT INTO `TestProducts` (`Id`, `Name`, `Price`, `IsAvailable`) VALUES", sql);
            Assert.Contains("(1, 'Product Alpha', 150.75, 1)", sql);
            Assert.Contains("SET FOREIGN_KEY_CHECKS = 1;", sql);
            Assert.DoesNotContain("GO", sql);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task SqlScriptExporter_ExportsPostgreSqlDialectCorrectly()
    {
        var exporter = new SqlScriptExporter();
        var table = CreateSampleTable();
        var rows = CreateSampleRows();
        string tempPath = Path.Combine(Path.GetTempPath(), $"test_pg_{Guid.NewGuid():N}.sql");

        try
        {
            var options = new ExportOptions { SqlDialect = SqlDialect.PostgreSql };
            await exporter.ExportAsync(table, rows, tempPath, options);
            Assert.True(File.Exists(tempPath));

            string sql = await File.ReadAllTextAsync(tempPath);
            Assert.Contains("CREATE TABLE IF NOT EXISTS \"dbo\".\"TestProducts\"", sql);
            Assert.Contains("\"Id\" INTEGER NOT NULL", sql);
            Assert.Contains("\"Name\" VARCHAR(100) NOT NULL", sql);
            Assert.Contains("\"Price\" NUMERIC(10,2) NOT NULL", sql);
            Assert.Contains("\"IsAvailable\" BOOLEAN NOT NULL", sql);
            Assert.Contains("INSERT INTO \"dbo\".\"TestProducts\" (\"Id\", \"Name\", \"Price\", \"IsAvailable\") VALUES", sql);
            Assert.Contains("(1, 'Product Alpha', 150.75, TRUE)", sql);
            Assert.DoesNotContain("GO", sql);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
