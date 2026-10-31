using System.Collections;

namespace SQLRecovery.Core.Engine.Allocation;

/// <summary>
/// Shared bitmap used by GAM, SGAM and IAM pages. The bitmap lives in the second slot's fixed
/// data and marks 8-page extents.
/// </summary>
public abstract class ExtentAllocationMap : PrimaryRecordPage
{
    public bool[] ExtentMap { get; } = new bool[63904];

    protected ExtentAllocationMap(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        ParseBitmap();
    }

    private void ParseBitmap()
    {
        if (Records.Length < 2 || !Records[1].IsValid)
            return;

        var bitmap = Records[1].FixedLengthData;
        int index = 0;

        foreach (byte b in bitmap)
        {
            var bits = new BitArray(new[] { b });

            for (int i = 0; i < 8 && index < ExtentMap.Length; i++)
                ExtentMap[index++] = bits[i];
        }
    }

    public IEnumerable<ExtentPointer> GetAllocatedExtents()
    {
        int rangeStart = Header.Pointer.PageID / 511232 * 511232;

        for (int i = 0; i < ExtentMap.Length; i++)
        {
            if (ExtentMap[i])
                yield return new ExtentPointer(new PagePointer(Header.Pointer.FileID, rangeStart + i * 8));
        }
    }

    public static PagePointer GetGamPointerForPage(PagePointer location) =>
        new(location.FileID, Math.Max(location.PageID / 511232 * 511232, 2));

    public static PagePointer GetSgamPointerForPage(PagePointer location) =>
        new(location.FileID, Math.Max(location.PageID / 511232 * 511232 + 1, 3));
}
