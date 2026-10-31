using System.Text;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// Page 9 of the primary data file: database metadata (internal version, name, first
/// sysindexes page). Modern files store the boot record in the page slot array while
/// SQL Server 7.0/2000 files keep it at a fixed offset near the start of the page.
/// </summary>
public sealed class BootPage : PrimaryRecordPage
{
    private static readonly int[] LegacyRecordOffsets = { 96, 100, 104 };

    public string DatabaseName { get; private set; } = string.Empty;
    public short InternalVersion { get; private set; }
    public short CreateVersion { get; private set; }
    public int Status { get; private set; }
    public int NextID { get; private set; }
    public short DBID { get; private set; }
    public long MaxDBTimeStamp { get; private set; }
    public PagePointer FirstSysIndexes { get; private set; }

    public BootPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        var bootRecord = Records
            .Where(r => r.IsValid)
            .Select(r => r.FixedLengthData)
            .FirstOrDefault(IsPlausibleRecord);

        bootRecord ??= ReadLegacyRecord(bytes);

        if (bootRecord is not null)
            ParseRecord(bootRecord);
    }

    private void ParseRecord(byte[] bootRecord)
    {
        if (bootRecord.Length < 4)
            return;

        InternalVersion = BitConverter.ToInt16(bootRecord, 0);
        CreateVersion = BitConverter.ToInt16(bootRecord, 2);

        if (bootRecord.Length >= 40)
        {
            Status = BitConverter.ToInt32(bootRecord, 32);
            NextID = BitConverter.ToInt32(bootRecord, 36);
        }

        if (bootRecord.Length >= 304)
        {
            var name = Encoding.Unicode.GetString(bootRecord, 48, 256);
            int nul = name.IndexOf('\0');

            if (nul >= 0)
                name = name[..nul];

            // The name field is fixed length and padded with 0x20 bytes, so an odd/even
            // alignment can decode the tail as U+2020 characters as well as spaces.
            DatabaseName = name.TrimEnd(' ', '\u2020', '\uFFFF');
        }

        if (bootRecord.Length >= 310)
            DBID = BitConverter.ToInt16(bootRecord, 308);

        if (bootRecord.Length >= 320)
            MaxDBTimeStamp = BitConverter.ToInt64(bootRecord, 312);

        if (bootRecord.Length >= 518)
            FirstSysIndexes = new PagePointer(BitConverter.ToInt16(bootRecord, 516), BitConverter.ToInt32(bootRecord, 512));
    }

    private static byte[]? ReadLegacyRecord(byte[] page)
    {
        foreach (int start in LegacyRecordOffsets)
        {
            if (start + 4 > page.Length)
                continue;

            int fixedLength = BitConverter.ToInt16(page, start + 2) - 4;

            if (fixedLength < 52 || start + 4 + fixedLength > page.Length)
                continue;

            var candidate = page.AsSpan(start + 4, fixedLength).ToArray();

            if (IsPlausibleRecord(candidate) && HasValidName(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsPlausibleRecord(byte[] data)
    {
        if (data.Length < 52)
            return false;

        short version = BitConverter.ToInt16(data, 0);
        return version is >= 500 and <= 1200;
    }

    private static bool HasValidName(byte[] data)
    {
        if (data.Length < 304)
            return false;

        var name = Encoding.Unicode.GetString(data, 48, 256);
        int nul = name.IndexOf('\0');

        if (nul >= 0)
            name = name[..nul];

        name = name.TrimEnd(' ', '\u2020', '\uFFFF');

        return name.Length is > 0 and < 129 && name.All(c => !char.IsControl(c));
    }

    public override string ToString() =>
        $"DatabaseName={DatabaseName}, Version={InternalVersion}, DBID={DBID}";
}
