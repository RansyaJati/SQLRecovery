namespace SQLRecovery.Core.SqlTypes;

/// <summary>Converts the raw bytes of a single column value into a .NET value.</summary>
public interface ISqlType
{
    bool IsVariableLength { get; }

    /// <summary>Number of bytes occupied in the fixed-length section, or null for varlen types.</summary>
    short? FixedLength { get; }

    /// <summary>Raw byte length for varlen columns, when known (max length).</summary>
    short MaxLength { get; }

    object? GetValue(byte[] value);
}
