using SQLRecovery.Core.Engine;

namespace SQLRecovery.Core.Schema;

/// <summary>A table (or internal/system table) with its physical layout.</summary>
public sealed class TableSchema
{
    public int ObjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SchemaName { get; set; } = "dbo";
    public string ObjectType { get; set; } = "U";
    public long RowCount { get; set; }

    public IReadOnlyList<ColumnDefinition> Columns { get; set; } = Array.Empty<ColumnDefinition>();
    public IReadOnlyList<IndexDefinition> Indexes { get; set; } = Array.Empty<IndexDefinition>();

    /// <summary>Index used to read rows (1 for clustered, 0 for heap).</summary>
    public int DataIndexId { get; set; }

    public PagePointer RootPage { get; set; }
    public PagePointer FirstPage { get; set; }
    public PagePointer FirstIamPage { get; set; }

    public bool IsHeap => DataIndexId == 0;
    public bool IsUserTable => ObjectType == "U";
    public string FullName => $"{SchemaName}.{Name}";

    public override string ToString() => $"{FullName} ({Columns.Count} columns, {RowCount:N0} rows)";
}
