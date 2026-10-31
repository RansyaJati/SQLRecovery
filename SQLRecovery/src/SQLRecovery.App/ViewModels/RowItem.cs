using System.IO;
using SQLRecovery.Core.Schema;

namespace SQLRecovery.App.ViewModels;

/// <summary>A single row exposed to the DataGrid through an integer indexer.</summary>
public sealed class RowItem
{
    public RowItem(object?[] values, long ordinal)
    {
        Values = values;
        Ordinal = ordinal;
    }

    public object?[] Values { get; }

    /// <summary>1-based row number within the loaded set.</summary>
    public long Ordinal { get; }

    public object? this[int index] =>
        (uint)index < (uint)Values.Length ? Values[index] : null;
}

/// <summary>Sidebar entry for a table.</summary>
public sealed class TableListItem
{
    public TableListItem(TableSchema table)
    {
        Table = table;
        Name = table.Name;
        SchemaName = table.SchemaName;
        FullName = table.FullName;
        RowCountText = table.IsUserTable
            ? $"{table.RowCount:N0} rows"
            : "system table";
        IsSystem = !table.IsUserTable;
    }

    public TableSchema Table { get; }

    public string Name { get; set; }

    public string SchemaName { get; set; }

    public string FullName { get; set; }

    public string RowCountText { get; set; }

    public bool IsSystem { get; set; }
}

/// <summary>Welcome-screen entry for a recently opened database.</summary>
public sealed class RecentFileItem
{
    public RecentFileItem(string path)
    {
        FilePath = path;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);

    public string Folder => Path.GetDirectoryName(FilePath) ?? string.Empty;

    public bool Exists => File.Exists(FilePath);
}
