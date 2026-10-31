using System.Text;

namespace SQLRecovery.Core.SqlTypes;

public sealed class SqlUniqueIdentifierType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 16;
    public override object GetValue(byte[] value) => new Guid(value);
}

public sealed class SqlXmlType : SqlTypeBase
{
    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength => -1;

    public override object GetValue(byte[] value) => Encoding.Unicode.GetString(value);
}

public sealed class SqlVariantType : SqlTypeBase
{
    public override bool IsVariableLength => true;
    public override short? FixedLength => null;
    public override short MaxLength => 8016;

    public override object GetValue(byte[] value) => Convert.ToHexString(value);
}
