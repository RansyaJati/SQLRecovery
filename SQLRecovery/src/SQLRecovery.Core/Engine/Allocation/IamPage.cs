namespace SQLRecovery.Core.Engine.Allocation;

/// <summary>
/// Index Allocation Map page: lists the extents that belong to a single index/heap allocation
/// unit and points at the next IAM page in the chain.
/// </summary>
public sealed class IamPage : ExtentAllocationMap
{
    public uint SequenceNumber { get; private set; }
    public short Status { get; private set; }
    public short IndexID { get; private set; }
    public int ObjectID { get; private set; }
    public byte PageCount { get; private set; }
    public PagePointer StartPage { get; private set; }

    /// <summary>Single-page allocations listed in the IAM header.</summary>
    public PagePointer[] Slots { get; } = new PagePointer[8];

    public IamPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        ParseHeader();
    }

    private void ParseHeader()
    {
        // SQL Server 2000 stores the 94-byte IAM header directly at page offset 96
        if (Database.Version < SqlServerVersion.SqlServer2005)
        {
            const int baseOffset = 96;
            // The 8 single-page allocation slots start at offset 140 (96 + 44)
            // Each slot is 6 bytes: fileId (2 bytes) followed by pageId (4 bytes)
            for (int i = 0; i < 8; i++)
            {
                int offset = baseOffset + 44 + i * 6;
                short fileId = BitConverter.ToInt16(RawBytes, offset);
                int pageId = BitConverter.ToInt32(RawBytes, offset + 2);
                Slots[i] = new PagePointer(fileId, pageId);
            }
            return;
        }

        if (Records.Length == 0 || !Records[0].IsValid)
            return;

        var header = Records[0].FixedLengthData;

        if (header.Length < 90)
            return;

        SequenceNumber = BitConverter.ToUInt32(header, 0);
        Status = BitConverter.ToInt16(header, 14);
        ObjectID = BitConverter.ToInt32(header, 28);
        IndexID = BitConverter.ToInt16(header, 32);
        PageCount = header[34];
        StartPage = new PagePointer(BitConverter.ToInt16(header, 40), BitConverter.ToInt32(header, 36));

        for (int i = 0; i < 8; i++)
        {
            int offset = 42 + i * 6;
            Slots[i] = new PagePointer(BitConverter.ToInt16(header, offset + 4), BitConverter.ToInt32(header, offset));
        }
    }

    /// <summary>Extents referenced by this IAM page, using the IAM's own file id.</summary>
    public new IEnumerable<ExtentPointer> GetAllocatedExtents()
    {
        int rangeStart = Header.Pointer.PageID / 511232 * 511232;
        short fileId = StartPage.IsZero ? Header.Pointer.FileID : StartPage.FileID;

        for (int i = 0; i < ExtentMap.Length; i++)
        {
            if (ExtentMap[i])
                yield return new ExtentPointer(new PagePointer(fileId, rangeStart + i * 8));
        }
    }
}
