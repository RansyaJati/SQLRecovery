using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Export;

/// <summary>
/// Maps SQL Server system data types to dialect-specific DDL type definitions
/// (SQL Server, MySQL / MariaDB, PostgreSQL).
/// </summary>
public static class SqlTypeMapper
{
    public static string GetColumnType(ColumnDefinition column, SqlDialect dialect) => dialect switch
    {
        SqlDialect.SqlServer => column.TypeDisplayName,
        SqlDialect.MySql => ToMySqlType(column),
        SqlDialect.PostgreSql => ToPostgreSqlType(column),
        _ => column.TypeDisplayName
    };

    private static string ToMySqlType(ColumnDefinition column) => column.Type switch
    {
        SqlDbType.TinyInt => "TINYINT",
        SqlDbType.SmallInt => "SMALLINT",
        SqlDbType.Int => "INT",
        SqlDbType.BigInt => "BIGINT",
        SqlDbType.Bit => "TINYINT(1)",
        SqlDbType.Real => "FLOAT",
        SqlDbType.Float => "DOUBLE",
        SqlDbType.Money or SqlDbType.SmallMoney => "DECIMAL(19,4)",
        SqlDbType.Decimal or SqlDbType.Numeric =>
            $"DECIMAL({(column.Precision > 0 ? column.Precision : 18)},{column.Scale})",
        SqlDbType.Date => "DATE",
        SqlDbType.Time => "TIME",
        SqlDbType.DateTime or SqlDbType.SmallDateTime or SqlDbType.DateTime2 => "DATETIME",
        SqlDbType.DateTimeOffset => "VARCHAR(35)",
        SqlDbType.Char => $"CHAR({Math.Max(1, (int)column.MaxLength)})",
        SqlDbType.NChar => $"CHAR({Math.Max(1, (int)column.MaxLength)})",
        SqlDbType.Varchar or SqlDbType.NVarChar =>
            column.MaxLength > 0 && column.MaxLength <= 8000 ? $"VARCHAR({column.MaxLength})" : "LONGTEXT",
        SqlDbType.Text or SqlDbType.NText or SqlDbType.Xml => "LONGTEXT",
        SqlDbType.Binary => $"BINARY({Math.Max(1, (int)column.MaxLength)})",
        SqlDbType.VarBinary => column.MaxLength > 0 && column.MaxLength <= 8000 ? $"VARBINARY({column.MaxLength})" : "LONGBLOB",
        SqlDbType.Image => "LONGBLOB",
        SqlDbType.UniqueIdentifier => "CHAR(36)",
        _ => "VARCHAR(255)"
    };

    private static string ToPostgreSqlType(ColumnDefinition column) => column.Type switch
    {
        SqlDbType.TinyInt or SqlDbType.SmallInt => "SMALLINT",
        SqlDbType.Int => "INTEGER",
        SqlDbType.BigInt => "BIGINT",
        SqlDbType.Bit => "BOOLEAN",
        SqlDbType.Real => "REAL",
        SqlDbType.Float => "DOUBLE PRECISION",
        SqlDbType.Money or SqlDbType.SmallMoney => "NUMERIC(19,4)",
        SqlDbType.Decimal or SqlDbType.Numeric =>
            $"NUMERIC({(column.Precision > 0 ? column.Precision : 18)},{column.Scale})",
        SqlDbType.Date => "DATE",
        SqlDbType.Time => "TIME",
        SqlDbType.DateTime or SqlDbType.SmallDateTime or SqlDbType.DateTime2 => "TIMESTAMP",
        SqlDbType.DateTimeOffset => "TIMESTAMPTZ",
        SqlDbType.Char or SqlDbType.NChar => $"CHAR({Math.Max(1, (int)column.MaxLength)})",
        SqlDbType.Varchar or SqlDbType.NVarChar =>
            column.MaxLength > 0 ? $"VARCHAR({column.MaxLength})" : "TEXT",
        SqlDbType.Text or SqlDbType.NText or SqlDbType.Xml => "TEXT",
        SqlDbType.Binary or SqlDbType.VarBinary or SqlDbType.Image => "BYTEA",
        SqlDbType.UniqueIdentifier => "UUID",
        _ => "TEXT"
    };
}
