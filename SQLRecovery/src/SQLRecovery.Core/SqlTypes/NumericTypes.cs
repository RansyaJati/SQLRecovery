using System.Data.SqlTypes;

namespace SQLRecovery.Core.SqlTypes;

public abstract class SqlTypeBase : ISqlType
{
    public abstract bool IsVariableLength { get; }
    public abstract short? FixedLength { get; }
    public virtual short MaxLength => FixedLength ?? 0;
    public abstract object? GetValue(byte[] value);
}

public sealed class SqlTinyInt : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 1;
    public override object GetValue(byte[] value) => value[0];
}

public sealed class SqlSmallInt : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 2;
    public override object GetValue(byte[] value) => BitConverter.ToInt16(value);
}

public sealed class SqlInt : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 4;
    public override object GetValue(byte[] value) => BitConverter.ToInt32(value);
}

public sealed class SqlBigInt : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 8;
    public override object GetValue(byte[] value) => BitConverter.ToInt64(value);
}

/// <summary>bit columns are bit-packed at the end of the fixed section; see TableRowReader.</summary>
public sealed class SqlBit : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => null;
    public override object GetValue(byte[] value) => value.Length > 0 && value[0] != 0;
}

public sealed class SqlDecimalType : SqlTypeBase
{
    private readonly byte _precision;
    private readonly byte _scale;

    public SqlDecimalType(byte precision, byte scale)
    {
        _precision = precision;
        _scale = scale;
    }

    public override bool IsVariableLength => false;

    public override short? FixedLength => (short)(1 + StorageInts(_precision) * 4);

    private static int StorageInts(byte precision) => precision switch
    {
        <= 9 => 1,
        <= 19 => 2,
        <= 28 => 3,
        _ => 4
    };

    public override object? GetValue(byte[] value)
    {
        if (value.Length < 5)
            return null;

        var ints = new int[4];
        for (int i = 0; i < StorageInts(_precision); i++)
            ints[i] = BitConverter.ToInt32(value, 1 + i * 4);

        var sqlDecimal = new SqlDecimal(_precision, _scale, value[0] != 0, ints);

        try
        {
            return sqlDecimal.Value;
        }
        catch (OverflowException)
        {
            return sqlDecimal.ToString();
        }
    }
}

public sealed class SqlMoneyType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 8;
    public override object GetValue(byte[] value) => BitConverter.ToInt64(value) / 10000m;
}

public sealed class SqlSmallMoneyType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 4;
    public override object GetValue(byte[] value) => BitConverter.ToInt32(value) / 10000m;
}

public sealed class SqlFloatType : SqlTypeBase
{
    private readonly short _length;

    public SqlFloatType(short length) => _length = length;

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;
    public override object GetValue(byte[] value) => _length == 4
        ? BitConverter.ToSingle(value)
        : BitConverter.ToDouble(value);
}

public sealed class SqlRealType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 4;
    public override object GetValue(byte[] value) => BitConverter.ToSingle(value);
}
