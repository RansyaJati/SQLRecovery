using System.Text;

namespace SQLRecovery.Core.SqlTypes;

public sealed class SqlCharType : SqlTypeBase
{
    private readonly short _length;
    private readonly Encoding _encoding;

    public SqlCharType(short length, Encoding? encoding = null)
    {
        _length = length;
        _encoding = encoding ?? Encoding.Latin1;
    }

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;
    public override object GetValue(byte[] value) => _encoding.GetString(value).TrimEnd(' ', '\0');
}

public sealed class SqlVarcharType : SqlTypeBase
{
    private readonly Encoding _encoding;

    public SqlVarcharType(short maxLength, Encoding? encoding = null)
    {
        MaxLength = maxLength;
        _encoding = encoding ?? Encoding.Latin1;
    }

    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength { get; }
    public override object GetValue(byte[] value) => _encoding.GetString(value);
}

public sealed class SqlNCharType : SqlTypeBase
{
    private readonly short _length;

    public SqlNCharType(short length) => _length = length;

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;
    public override object GetValue(byte[] value) => Encoding.Unicode.GetString(value).TrimEnd(' ', '\0');
}

public sealed class SqlNVarcharType : SqlTypeBase
{
    public SqlNVarcharType(short maxLength) => MaxLength = maxLength;

    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength { get; }
    public override object GetValue(byte[] value) => Encoding.Unicode.GetString(value);
}

/// <summary>
/// text / ntext / image / (max) types. The in-row value is a 16 byte LOB pointer which is not
/// resolved yet; the raw pointer bytes are returned so callers can decide how to handle it.
/// </summary>
public sealed class SqlLobType : SqlTypeBase
{
    private readonly bool _isText;

    public SqlLobType(bool isText) => _isText = isText;

    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength => -1;
    public bool IsText => _isText;

    public override object GetValue(byte[] value)
    {
        // When the value fits inline SQL Server stores the bytes directly (no 16 byte pointer).
        if (value.Length == 16)
            return new LobPointer(value);

        return value;
    }
}

/// <summary>An unresolved 16-byte LOB pointer (textptr / imageptr).</summary>
public sealed class LobPointer
{
    public byte[] Raw { get; }

    public LobPointer(byte[] raw) => Raw = raw;

    public override string ToString() =>
        "<LOB:" + Convert.ToHexString(Raw.AsSpan(0, Math.Min(8, Raw.Length))) + ">";
}
