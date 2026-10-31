namespace SQLRecovery.Core.Schema;

/// <summary>An index (or heap) belonging to a table.</summary>
public sealed class IndexDefinition
{
    public int IndexId { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte Type { get; set; }
    public bool IsClustered => IndexId == 1;
    public bool IsHeap => IndexId == 0;
    public long RowCount { get; set; }
    public long Rowset { get; set; }
    public Engine.PagePointer RootPage { get; set; }
    public Engine.PagePointer FirstPage { get; set; }
    public Engine.PagePointer FirstIamPage { get; set; }

    public override string ToString() => $"{Name} (indid {IndexId})";
}
