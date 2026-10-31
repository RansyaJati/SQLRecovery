namespace SQLRecovery.Core.SqlTypes;

public sealed class SqlBinaryType : SqlTypeBase
{
    private readonly short _length;

    public SqlBinaryType(short length) => _length = length;

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;
    public override object GetValue(byte[] value) => value;
}

public sealed class SqlVarBinaryType : SqlTypeBase
{
    public SqlVarBinaryType(short maxLength) => MaxLength = maxLength;

    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength { get; }
    public override object GetValue(byte[] value) => value;
}

public sealed class SqlTimestampType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 8;
    public override object GetValue(byte[] value) => Convert.ToHexString(value);
}
