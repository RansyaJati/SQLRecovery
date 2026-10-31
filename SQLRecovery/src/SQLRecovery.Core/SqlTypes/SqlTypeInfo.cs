using SQLRecovery.Core.Schema;

namespace SQLRecovery.Core.SqlTypes;

/// <summary>Static metadata about SQL Server data types (storage size, variable length).</summary>
public static class SqlTypeInfo
{
    public static bool IsVariableLength(SqlDbType type) => type switch
    {
        SqlDbType.Varchar or SqlDbType.NVarChar or SqlDbType.VarBinary => true,
        SqlDbType.Text or SqlDbType.NText or SqlDbType.Image => true,
        SqlDbType.Xml or SqlDbType.SqlVariant or SqlDbType.ClrType => true,
        _ => false
    };

    public static bool IsVariableLength(SqlDbType type, short maxLength) =>
        IsVariableLength(type) || maxLength < 0;

    public static short? FixedLength(SqlDbType type, short maxLength, byte precision, byte scale) => type switch
    {
        SqlDbType.TinyInt => 1,
        SqlDbType.SmallInt => 2,
        SqlDbType.Int => 4,
        SqlDbType.BigInt => 8,
        SqlDbType.Bit => null, // bit-packed; handled by TableRowReader
        SqlDbType.Money => 8,
        SqlDbType.SmallMoney => 4,
        SqlDbType.Real => 4,
        SqlDbType.Float => maxLength == 4 ? (short)4 : (short)8,
        SqlDbType.DateTime => 8,
        SqlDbType.SmallDateTime => 4,
        SqlDbType.Date => 3,
        SqlDbType.Time => (short)(3 + (scale <= 2 ? 0 : scale <= 4 ? 1 : 2)),
        SqlDbType.DateTime2 => (short)(6 + (scale <= 2 ? 0 : scale <= 4 ? 1 : 2)),
        SqlDbType.DateTimeOffset => (short)(8 + (scale <= 2 ? 0 : scale <= 4 ? 1 : 2)),
        SqlDbType.UniqueIdentifier => 16,
        SqlDbType.Timestamp => 8,
        SqlDbType.Char or SqlDbType.NChar or SqlDbType.Binary => maxLength == -1 ? null : maxLength,
        SqlDbType.Decimal or SqlDbType.Numeric => (short)(1 + StorageInts(precision) * 4),
        _ => null
    };

    public static int StorageInts(byte precision) => precision switch
    {
        <= 9 => 1,
        <= 19 => 2,
        <= 28 => 3,
        _ => 4
    };

    public static string DisplayName(SqlDbType type, short maxLength, byte precision, byte scale)
    {
        string baseName = type switch
        {
            SqlDbType.BigInt => "bigint",
            SqlDbType.Int => "int",
            SqlDbType.SmallInt => "smallint",
            SqlDbType.TinyInt => "tinyint",
            SqlDbType.Bit => "bit",
            SqlDbType.Decimal => "decimal",
            SqlDbType.Numeric => "numeric",
            SqlDbType.Money => "money",
            SqlDbType.SmallMoney => "smallmoney",
            SqlDbType.Float => "float",
            SqlDbType.Real => "real",
            SqlDbType.DateTime => "datetime",
            SqlDbType.SmallDateTime => "smalldatetime",
            SqlDbType.Date => "date",
            SqlDbType.Time => "time",
            SqlDbType.DateTime2 => "datetime2",
            SqlDbType.DateTimeOffset => "datetimeoffset",
            SqlDbType.UniqueIdentifier => "uniqueidentifier",
            SqlDbType.Char => "char",
            SqlDbType.Varchar => "varchar",
            SqlDbType.NChar => "nchar",
            SqlDbType.NVarChar => "nvarchar",
            SqlDbType.Binary => "binary",
            SqlDbType.VarBinary => "varbinary",
            SqlDbType.Timestamp => "rowversion",
            SqlDbType.Text => "text",
            SqlDbType.NText => "ntext",
            SqlDbType.Image => "image",
            SqlDbType.Xml => "xml",
            SqlDbType.SqlVariant => "sql_variant",
            _ => "unknown"
        };

        return type switch
        {
            SqlDbType.Decimal or SqlDbType.Numeric => $"{baseName}({precision},{scale})",
            SqlDbType.Char or SqlDbType.Varchar => maxLength < 0 ? $"{baseName}(max)" : $"{baseName}({maxLength})",
            SqlDbType.NChar or SqlDbType.NVarChar => maxLength < 0 ? $"{baseName}(max)" : $"{baseName}({maxLength / 2})",
            SqlDbType.Binary or SqlDbType.VarBinary => maxLength < 0 ? $"{baseName}(max)" : $"{baseName}({maxLength})",
            SqlDbType.Time or SqlDbType.DateTime2 or SqlDbType.DateTimeOffset => $"{baseName}({scale})",
            SqlDbType.Text or SqlDbType.NText or SqlDbType.Image or SqlDbType.Xml or SqlDbType.SqlVariant => baseName,
            _ => baseName
        };
    }
}
