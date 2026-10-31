namespace SQLRecovery.Core.Engine;

/// <summary>
/// A page that contains a row offset (slot) array growing backwards from the end of the page.
/// </summary>
public class RecordPage : Page
{
    /// <summary>Row offsets indexed by slot id, as stored at the bottom of the page.</summary>
    public short[] SlotArray { get; }

    public RecordPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        SlotArray = new short[Math.Max(0, (int)Header.SlotCnt)];

        for (int i = 0; i < SlotArray.Length; i++)
            SlotArray[i] = BitConverter.ToInt16(RawBytes, RawBytes.Length - i * 2 - 2);
    }
}
