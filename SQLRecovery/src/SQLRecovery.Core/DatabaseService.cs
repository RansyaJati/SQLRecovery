using SQLRecovery.Core.Catalog;
using SQLRecovery.Core.Engine;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Core;

/// <summary>
/// High level facade: open a database, read its catalog, build table schemas and enumerate rows.
/// </summary>
public sealed class DatabaseService : IDisposable
{
    private readonly CatalogReader _catalog;
    private readonly TableDataReader _rowReader;

    public SqlDatabase Database { get; }
    public CatalogReader Catalog => _catalog;
    public DatabaseInfo Info { get; }
    public IReadOnlyList<TableSchema> Tables { get; }

    private DatabaseService(SqlDatabase database, CatalogReader catalog, IReadOnlyList<TableSchema> tables)
    {
        Database = database;
        _catalog = catalog;
        Tables = tables;
        _rowReader = new TableDataReader(database);

        Info = new DatabaseInfo
        {
            Name = database.Name,
            FilePath = string.Join("; ", database.DataFiles.Select(f => f.Path)),
            Version = database.Version,
            InternalVersion = database.InternalVersion,
            DatabaseId = database.DatabaseId,
            TotalPages = database.TotalPages,
            FileCount = database.AllFiles.Count(),
            FileSizeBytes = database.AllFiles.Sum(f => f.Length),
            TableCount = tables.Count
        };
    }

    public static DatabaseService Open(string path) => Open(new[] { path });

    public static DatabaseService Open(IEnumerable<string> paths)
    {
        var database = SqlDatabase.Open(paths);

        try
        {
            var catalog = new CatalogReader(database);
            var tables = BuildTables(catalog);
            return new DatabaseService(database, catalog, tables);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    public IEnumerable<object?[]> ReadRows(TableSchema table, bool includeDeleted = false) =>
        _rowReader.ReadRows(table, includeDeleted);

    private static IReadOnlyList<TableSchema> BuildTables(CatalogReader catalog)
    {
        if (!catalog.IsAvailable)
            return Array.Empty<TableSchema>();

        var schemaNames = catalog.Objects
            .GroupBy(o => o.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);

        var tables = new List<TableSchema>();

        foreach (var obj in catalog.Objects)
        {
            var type = obj.Type.Trim();

            if (type is not ("U" or "S" or "IT"))
                continue;

            var columns = catalog.Columns
                .Where(c => c.Id == obj.Id && c.Number == 0)
                .OrderBy(c => c.ColId)
                .Select(c => MapColumn(c, catalog.IsSql2000))
                .ToList();

            if (columns.Count == 0)
                continue;

            var indexes = catalog.Indexes
                .Where(i => i.Id == obj.Id)
                .Select(i => new IndexDefinition
                {
                    IndexId = i.IndId,
                    Name = string.IsNullOrWhiteSpace(i.Name) ? (i.IndId == 0 ? "Heap" : $"Index_{i.IndId}") : i.Name,
                    Type = i.Type,
                    Rowset = i.Rowset
                } as IndexDefinition)
                .ToList();

            int dataIndexId = indexes.Any(i => i.IndexId == 1) ? 1 : 0;

            var rowset = catalog.Rowsets.FirstOrDefault(r => r.IdMajor == obj.Id && r.IdMinor == dataIndexId)
                ?? catalog.Rowsets.FirstOrDefault(r => r.IdMajor == obj.Id);

            if (rowset is null)
                continue;

            var allocUnit = catalog.AllocationUnits.FirstOrDefault(a => a.OwnerId == rowset.RowsetId && a.Type == 1)
                ?? catalog.AllocationUnits.FirstOrDefault(a => a.Auid == rowset.RowsetId && a.Type == 1);

            string schemaName = obj.Nsid <= 1
                ? "dbo"
                : schemaNames.TryGetValue(obj.Nsid, out var name) && !string.IsNullOrWhiteSpace(name)
                    ? name
                    : "dbo";

            var table = new TableSchema
            {
                ObjectId = obj.Id,
                Name = string.IsNullOrWhiteSpace(obj.Name) ? $"object_{obj.Id}" : obj.Name,
                SchemaName = schemaName,
                ObjectType = type,
                RowCount = rowset.RowCount,
                Columns = columns,
                Indexes = indexes,
                DataIndexId = dataIndexId,
                RootPage = allocUnit?.RootPage ?? PagePointer.Zero,
                FirstPage = allocUnit?.FirstPage ?? PagePointer.Zero,
                FirstIamPage = allocUnit?.FirstIamPage ?? PagePointer.Zero
            };

            tables.Add(table);
        }

        return tables
            .OrderBy(t => t.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ColumnDefinition MapColumn(SysColumn col, bool isSql2000 = false)
    {
        var type = (SqlDbType)col.XType;

        if (!Enum.IsDefined(type))
            type = SqlDbType.Unknown;

        bool isNullable = isSql2000
            ? (col.Status & 8) != 0 || (col.Status & 1) == 0
            : (col.Status & 1) == 0;

        return new ColumnDefinition
        {
            ColumnId = col.ColId,
            Name = string.IsNullOrWhiteSpace(col.Name) ? $"column_{col.ColId}" : col.Name,
            Type = type,
            MaxLength = col.Length,
            Precision = col.Prec,
            Scale = col.Scale,
            CollationId = col.CollationId,
            IsNullable = isNullable,
            IsIdentity = isSql2000 ? (col.Status & 0x80) != 0 : (col.Status & 4) != 0,
            IsRowGuidCol = !isSql2000 && (col.Status & 8) != 0,
            IsComputed = !isSql2000 && (col.Status & 16) != 0,
            IsSparse = !isSql2000 && (col.Status & 0x1000000) != 0,
            RecordOffset = col.Offset
        };
    }

    public void Dispose() => Database.Dispose();
}
