namespace SQLRecovery.Core.Engine.Allocation;

[Flags]
public enum PfsFlags : byte
{
    Allocated = 0x01,
    MixedExtent = 0x02,
    IAM = 0x04,
    GhostRecords = 0x08,
    UpTo50 = 0x20,
    UpTo80 = 0x40,
    UpTo95 = 0x60,
    UpTo100 = 0x80
}

/// <summary>
/// Page Free Space page: one status byte per page describing whether it is allocated and how
/// full it is.
/// </summary>
public sealed class PfsPage : Page
{
    public const int PagesPerPfs = 8088;

    public PfsPage(byte[] bytes, SqlDatabase database) : base(bytes, database) { }

    public static PagePointer GetPfsPointerForPage(PagePointer location) =>
        new(location.FileID, Math.Max(location.PageID / PagesPerPfs * PagesPerPfs, 1));

    public byte GetStatusByte(int pageId)
    {
        int basePage = Header.Pointer.PageID == 1 ? 0 : Header.Pointer.PageID;
        int index = pageId - basePage;

        if (index < 0 || index >= PagesPerPfs)
            return 0;

        return RawBytes[PageHeader.Size + 4 + index];
    }

    public bool IsAllocated(int pageId) => (GetStatusByte(pageId) & (byte)PfsFlags.Allocated) != 0;

    public bool IsIamPage(int pageId) => (GetStatusByte(pageId) & (byte)PfsFlags.IAM) != 0;

    public bool HasGhostRecords(int pageId) => (GetStatusByte(pageId) & (byte)PfsFlags.GhostRecords) != 0;

    public byte Fullness(int pageId) => (byte)(GetStatusByte(pageId) & 0xE0) switch
    {
        0x20 => 50,
        0x40 => 80,
        0x60 => 95,
        0x80 => 100,
        _ => 0
    };
}
