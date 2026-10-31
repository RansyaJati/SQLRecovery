namespace SQLRecovery.Core.SqlTypes;

/// <summary>
/// SQL Server system type ids as stored in the <c>xtype</c> column of the system catalogs.
/// </summary>
public enum SqlDbType : byte
{
    Unknown = 0,
    Image = 34,
    Text = 35,
    UniqueIdentifier = 36,
    Date = 40,
    Time = 41,
    DateTime2 = 42,
    DateTimeOffset = 43,
    TinyInt = 48,
    SmallInt = 52,
    Int = 56,
    SmallDateTime = 58,
    Real = 59,
    Money = 60,
    DateTime = 61,
    Float = 62,
    SqlVariant = 98,
    NText = 99,
    Bit = 104,
    Decimal = 106,
    Numeric = 108,
    SmallMoney = 122,
    BigInt = 127,
    VarBinary = 165,
    Varchar = 167,
    Binary = 173,
    Char = 175,
    Timestamp = 189,
    NVarChar = 231,
    NChar = 239,
    ClrType = 240,
    Xml = 241
}
