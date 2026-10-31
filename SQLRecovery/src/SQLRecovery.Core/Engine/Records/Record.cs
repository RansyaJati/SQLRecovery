using System.Collections;

namespace SQLRecovery.Core.Engine.Records;

/// <summary>
/// Common state for a row record stored on a page.
/// </summary>
public abstract class Record
{
    public RecordType Type { get; protected set; }
    public bool HasNullBitmap { get; protected set; }
    public bool HasVariableLengthColumns { get; protected set; }
    public bool HasVersioningInformation { get; protected set; }

    public byte[] FixedLengthData { get; protected set; } = Array.Empty<byte>();
    public short NumberOfColumns { get; protected set; }
    public BitArray? NullBitmap { get; protected set; }
    public short NumberOfVariableLengthColumns { get; protected set; }

    /// <summary>Raw bytes of the complete record.</summary>
    public byte[] RawBytes { get; protected set; } = Array.Empty<byte>();

    /// <summary>Variable length column payloads keyed by ordinal position among varlen columns.</summary>
    public IDictionary<int, byte[]> VariableLengthColumnData { get; } = new Dictionary<int, byte[]>();

    /// <summary>Ordinals of varlen columns whose high bit was set (LOB pointer / complex / sparse / overflow).</summary>
    public ISet<int> ComplexColumns { get; } = new HashSet<int>();

    public Page Page { get; }

    /// <summary>False when the slot pointed outside the page or the record could not be parsed.</summary>
    public bool IsValid { get; protected set; }

    protected Record(Page page)
    {
        Page = page;
    }

    /// <summary>True when the column at <paramref name="columnIndex"/> is NULL.</summary>
    public bool IsNull(int columnIndex)
    {
        if (NullBitmap is null || columnIndex < 0 || columnIndex >= NullBitmap.Length)
            return false;

        return NullBitmap[columnIndex];
    }

    protected void ParseStatusBitsA(byte bits)
    {
        // bit 0: versioning tag (not used in 2008+)
        Type = (RecordType)((bits >> 1) & 0x07);
        HasNullBitmap = (bits & 0x10) != 0;
        HasVariableLengthColumns = (bits & 0x20) != 0;
        HasVersioningInformation = (bits & 0x40) != 0;
    }

    protected void ParseNullBitmap(ReadOnlySpan<byte> bytes, ref int offset)
    {
        int byteCount = (NumberOfColumns + 7) / 8;
        var bitmap = new BitArray(bytes.Slice(offset, byteCount).ToArray());
        NullBitmap = bitmap;
        offset += byteCount;
    }

    protected void ParseVariableLengthColumns(ReadOnlySpan<byte> bytes, ref int offset)
    {
        // When there is no fixed-length data and no null bitmap, the column count is implicit.
        if (FixedLengthData.Length == 0 && !HasNullBitmap)
            NumberOfVariableLengthColumns = NumberOfColumns;
        else
        {
            NumberOfVariableLengthColumns = BitConverter.ToInt16(bytes[offset..]);
            offset += 2;
        }

        if (NumberOfVariableLengthColumns < 0)
        {
            NumberOfVariableLengthColumns = 0;
            return;
        }

        var endOffsets = new int[NumberOfVariableLengthColumns];
        for (int i = 0; i < endOffsets.Length; i++)
        {
            endOffsets[i] = BitConverter.ToInt16(bytes[offset..]);
            offset += 2;
        }

        for (int i = 0; i < endOffsets.Length; i++)
        {
            bool isComplex = false;
            int endOffset = endOffsets[i];

            if ((endOffset & 0x8000) != 0)
            {
                endOffset &= 0x7FFF;
                isComplex = true;
                ComplexColumns.Add(i);
            }

            _ = isComplex;

            if (endOffset < offset || endOffset > bytes.Length)
            {
                // Corrupt offset - stop parsing remaining columns.
                VariableLengthColumnData[i] = Array.Empty<byte>();
                break;
            }

            VariableLengthColumnData[i] = bytes[offset..endOffset].ToArray();
            offset = endOffset;
        }
    }
}
