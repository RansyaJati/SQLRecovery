using SQLRecovery.Core.Engine.Records;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// A record page whose slots contain primary (data/index) records.
/// </summary>
public class PrimaryRecordPage : RecordPage
{
    public PrimaryRecord[] Records { get; }

    public PrimaryRecordPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        Records = new PrimaryRecord[SlotArray.Length];

        for (int i = 0; i < SlotArray.Length; i++)
        {
            int offset = SlotArray[i];

            if (offset <= 0 || offset >= RawBytes.Length)
            {
                Records[i] = new PrimaryRecord(ReadOnlyMemory<byte>.Empty, this, i, isValid: false);
                continue;
            }

            Records[i] = new PrimaryRecord(
                RawBytes.AsMemory(offset, RawBytes.Length - offset),
                this,
                i,
                isValid: true);
        }
    }
}
