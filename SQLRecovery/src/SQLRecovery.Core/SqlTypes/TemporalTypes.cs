namespace SQLRecovery.Core.SqlTypes;

public static class TemporalUtil
{
    public const double ClockTicksPerMs = 10d / 3d;

    /// <summary>Reads the time part of time/datetime2/datetimeoffset (stored little-endian).</summary>
    public static long ReadTimeUnits(ReadOnlySpan<byte> bytes)
    {
        long raw = 0;
        for (int i = bytes.Length - 1; i >= 0; i--)
            raw = (raw << 8) | bytes[i];
        return raw;
    }

    public static TimeSpan TimeFromUnits(ReadOnlySpan<byte> bytes, byte scale)
    {
        long units = ReadTimeUnits(bytes);
        long ticksPerUnit = scale >= 7 ? 1 : 10_000_000L / Pow10(scale);
        return TimeSpan.FromTicks(units * ticksPerUnit);
    }

    private static long Pow10(byte scale)
    {
        long result = 1;
        for (int i = 0; i < scale; i++)
            result *= 10;
        return result;
    }

    public static DateTime DateFromDays(ReadOnlySpan<byte> threeBytes)
    {
        int days = threeBytes[0] | (threeBytes[1] << 8) | (threeBytes[2] << 16);
        return new DateTime(1, 1, 1).AddDays(days);
    }

    public static int TimeByteCount(byte scale) => scale switch
    {
        <= 2 => 3,
        <= 4 => 4,
        _ => 5
    };
}

public sealed class SqlDateTimeType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 8;

    public override object GetValue(byte[] value)
    {
        int time = BitConverter.ToInt32(value, 0);
        int days = BitConverter.ToInt32(value, 4);
        return new DateTime(1900, 1, 1)
            .AddMilliseconds(time * TemporalUtil.ClockTicksPerMs)
            .AddDays(days);
    }
}

public sealed class SqlSmallDateTimeType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 4;

    public override object GetValue(byte[] value)
    {
        ushort minutes = BitConverter.ToUInt16(value, 0);
        ushort days = BitConverter.ToUInt16(value, 2);
        return new DateTime(1900, 1, 1, minutes / 60, minutes % 60, 0).AddDays(days);
    }
}

public sealed class SqlDateType : SqlTypeBase
{
    public override bool IsVariableLength => false;
    public override short? FixedLength => 3;
    public override object GetValue(byte[] value) => TemporalUtil.DateFromDays(value);
}

public sealed class SqlTimeType : SqlTypeBase
{
    private readonly byte _scale;
    private readonly short _length;

    public SqlTimeType(byte scale)
    {
        _scale = scale;
        _length = (short)TemporalUtil.TimeByteCount(scale);
    }

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;
    public override object GetValue(byte[] value) => TemporalUtil.TimeFromUnits(value, _scale);
}

public sealed class SqlDateTime2Type : SqlTypeBase
{
    private readonly byte _scale;
    private readonly short _length;

    public SqlDateTime2Type(byte scale)
    {
        _scale = scale;
        _length = (short)(TemporalUtil.TimeByteCount(scale) + 3);
    }

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;

    public override object GetValue(byte[] value)
    {
        int timeBytes = TemporalUtil.TimeByteCount(_scale);
        var time = TemporalUtil.TimeFromUnits(value.AsSpan(0, timeBytes), _scale);
        var date = TemporalUtil.DateFromDays(value.AsSpan(timeBytes, 3));
        return date.Add(time);
    }
}

public sealed class SqlDateTimeOffsetType : SqlTypeBase
{
    private readonly byte _scale;
    private readonly short _length;

    public SqlDateTimeOffsetType(byte scale)
    {
        _scale = scale;
        _length = (short)(TemporalUtil.TimeByteCount(scale) + 3 + 2);
    }

    public override bool IsVariableLength => false;
    public override short? FixedLength => _length;

    public override object GetValue(byte[] value)
    {
        int timeBytes = TemporalUtil.TimeByteCount(_scale);
        var time = TemporalUtil.TimeFromUnits(value.AsSpan(0, timeBytes), _scale);
        var date = TemporalUtil.DateFromDays(value.AsSpan(timeBytes, 3));
        short offsetMinutes = BitConverter.ToInt16(value, timeBytes + 3);
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        return new DateTimeOffset(date.Add(time), offset);
    }
}
