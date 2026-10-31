using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Core.Schema;

/// <summary>A single column of a table as stored in the system catalog.</summary>
public sealed class ColumnDefinition
{
    public int ColumnId { get; set; }
    public string Name { get; set; } = "?";
    public SqlDbType Type { get; set; }
    public short MaxLength { get; set; }
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public bool IsNullable { get; set; }
    public int CollationId { get; set; }
    public bool IsComputed { get; set; }
    public bool IsIdentity { get; set; }
    public bool IsSparse { get; set; }
    public bool IsRowGuidCol { get; set; }
    public short RecordOffset { get; set; }

    public bool IsVariableLength => SqlTypeInfo.IsVariableLength(Type, MaxLength);

    public short? FixedLength => SqlTypeInfo.FixedLength(Type, MaxLength, Precision, Scale);

    public string TypeDisplayName => SqlTypeInfo.DisplayName(Type, MaxLength, Precision, Scale);

    public override string ToString() => $"{Name} {TypeDisplayName}" + (IsNullable ? " NULL" : " NOT NULL");
}
