using System.Text;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// The 96-byte header present on the start of every SQL Server page.
/// Layout verified against OrcaMDF / DBCC PAGE documentation.
/// </summary>
public sealed class PageHeader
{
    public const int Size = 96;

    public byte HeaderVersion { get; }
    public PageType Type { get; }
    public byte TypeFlagBits { get; }
    public byte Level { get; }
    public short FlagBits { get; }
    public short IndexID { get; }
    public PagePointer PreviousPage { get; }
    public short Pminlen { get; }
    public PagePointer NextPage { get; }
    public short SlotCnt { get; }
    public int ObjectID { get; }
    public short FreeCnt { get; }
    public short FreeData { get; }
    public PagePointer Pointer { get; }
    public short ReservedCnt { get; }
    public string Lsn { get; }
    public short XactReserved { get; }
    public string XdesID { get; }
    public short GhostRecCnt { get; }
    public int Checksum { get; }

    public PageHeader(ReadOnlySpan<byte> header)
    {
        if (header.Length < Size)
            throw new ArgumentException($"Page header must be at least {Size} bytes.", nameof(header));

        HeaderVersion = header[0];
        Type = (PageType)header[1];
        TypeFlagBits = header[2];
        Level = header[3];
        FlagBits = BitConverter.ToInt16(header[4..6]);
        IndexID = BitConverter.ToInt16(header[6..8]);
        PreviousPage = new PagePointer(BitConverter.ToInt16(header[12..14]), BitConverter.ToInt32(header[8..12]));
        Pminlen = BitConverter.ToInt16(header[14..16]);
        NextPage = new PagePointer(BitConverter.ToInt16(header[20..22]), BitConverter.ToInt32(header[16..20]));
        SlotCnt = BitConverter.ToInt16(header[22..24]);
        ObjectID = BitConverter.ToInt32(header[24..28]);
        FreeCnt = BitConverter.ToInt16(header[28..30]);
        FreeData = BitConverter.ToInt16(header[30..32]);
        Pointer = new PagePointer(BitConverter.ToInt16(header[36..38]), BitConverter.ToInt32(header[32..36]));
        ReservedCnt = BitConverter.ToInt16(header[38..40]);
        Lsn = $"({BitConverter.ToInt32(header[40..44])}:{BitConverter.ToInt32(header[44..48])}:{BitConverter.ToInt16(header[48..50])})";
        XactReserved = BitConverter.ToInt16(header[50..52]);
        XdesID = $"({BitConverter.ToInt16(header[56..58])}:{BitConverter.ToInt32(header[52..56])})";
        GhostRecCnt = BitConverter.ToInt16(header[58..60]);
        Checksum = BitConverter.ToInt32(header[60..64]);
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"m_type:\t\t{Type}");
        sb.AppendLine($"m_level:\t{Level}");
        sb.AppendLine($"m_objId:\t{ObjectID}");
        sb.AppendLine($"m_indexId:\t{IndexID}");
        sb.AppendLine($"m_pageId:\t{Pointer}");
        sb.AppendLine($"m_prevPage:\t{PreviousPage}");
        sb.AppendLine($"m_nextPage:\t{NextPage}");
        sb.AppendLine($"m_slotCnt:\t{SlotCnt}");
        sb.AppendLine($"m_freeCnt:\t{FreeCnt}");
        sb.AppendLine($"m_freeData:\t{FreeData}");
        sb.AppendLine($"m_pminlen:\t{Pminlen}");
        sb.AppendLine($"m_ghostRecCnt:\t{GhostRecCnt}");
        sb.AppendLine($"m_lsn:\t\t{Lsn}");
        return sb.ToString();
    }
}
