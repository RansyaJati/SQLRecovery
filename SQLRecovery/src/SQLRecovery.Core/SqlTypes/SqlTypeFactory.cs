using SQLRecovery.Core.Schema;

namespace SQLRecovery.Core.SqlTypes;

public static class SqlTypeFactory
{
    public static ISqlType Create(ColumnDefinition column)
    {
        var encoding = CollationHelper.GetEncoding(column.CollationId);

        return column.Type switch
        {
            SqlDbType.TinyInt => new SqlTinyInt(),
            SqlDbType.SmallInt => new SqlSmallInt(),
            SqlDbType.Int => new SqlInt(),
            SqlDbType.BigInt => new SqlBigInt(),
            SqlDbType.Bit => new SqlBit(),
            SqlDbType.Decimal or SqlDbType.Numeric => new SqlDecimalType(column.Precision, column.Scale),
            SqlDbType.Money => new SqlMoneyType(),
            SqlDbType.SmallMoney => new SqlSmallMoneyType(),
            SqlDbType.Float => new SqlFloatType(column.MaxLength == 4 ? (short)4 : (short)8),
            SqlDbType.Real => new SqlRealType(),
            SqlDbType.DateTime => new SqlDateTimeType(),
            SqlDbType.SmallDateTime => new SqlSmallDateTimeType(),
            SqlDbType.Date => new SqlDateType(),
            SqlDbType.Time => new SqlTimeType(column.Scale),
            SqlDbType.DateTime2 => new SqlDateTime2Type(column.Scale),
            SqlDbType.DateTimeOffset => new SqlDateTimeOffsetType(column.Scale),
            SqlDbType.UniqueIdentifier => new SqlUniqueIdentifierType(),
            SqlDbType.Char => new SqlCharType(column.MaxLength, encoding),
            SqlDbType.Varchar => new SqlVarcharType(column.MaxLength, encoding),
            SqlDbType.NChar => new SqlNCharType(column.MaxLength),
            SqlDbType.NVarChar => new SqlNVarcharType(column.MaxLength),
            SqlDbType.Binary => new SqlBinaryType(column.MaxLength),
            SqlDbType.VarBinary => new SqlVarBinaryType(column.MaxLength),
            SqlDbType.Timestamp => new SqlTimestampType(),
            SqlDbType.Text or SqlDbType.NText => new SqlLobType(isText: true),
            SqlDbType.Image => new SqlLobType(isText: false),
            SqlDbType.Xml => new SqlXmlType(),
            SqlDbType.SqlVariant => new SqlVariantType(),
            _ => new SqlVariantType()
        };
    }
}
