namespace SQLRecovery.Core.Engine;

/// <summary>
/// A record stored on a text/blob page (page type 5). Text records only carry fixed length
/// data: a blob id, a structure type and either the payload or pointers to further structures.
/// </summary>
public sealed class TextRecord
{
    public byte[] FixedLengthData { get; } = Array.Empty<byte>();

    public bool IsValid { get; }

    public TextRecord(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
            return;

        try
        {
            int offset = 2; // status bits A and B
            int fixedLength = BitConverter.ToInt16(bytes[offset..]) - 4;
            offset += 2;

            if (fixedLength <= 0 || offset + fixedLength > bytes.Length)
                return;

            FixedLengthData = bytes.Slice(offset, fixedLength).ToArray();
            IsValid = true;
        }
        catch
        {
            IsValid = false;
        }
    }
}

/// <summary>A page holding text/blob records, addressed by the slot array at the end of the page.</summary>
public sealed class TextMixPage : RecordPage
{
    public TextRecord[] Records { get; }

    public TextMixPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        Records = new TextRecord[SlotArray.Length];

        for (int i = 0; i < SlotArray.Length; i++)
        {
            int offset = SlotArray[i];

            Records[i] = offset > 0 && offset < RawBytes.Length
                ? new TextRecord(RawBytes.AsSpan(offset))
                : new TextRecord(ReadOnlySpan<byte>.Empty);
        }
    }
}

/// <summary>
/// Resolves the 16 byte in-row LOB pointers used by text/ntext/image and (max) columns by
/// walking the text/blob page structures (SMALL_ROOT, DATA, INTERNAL, LARGE_ROOT_YUKON).
/// </summary>
public static class LobReader
{
    private const int MaxDepth = 16;
    private const long MaxTotalBytes = 256L * 1024 * 1024;

    private const short SmallRoot = 0;
    private const short Internal = 2;
    private const short Data = 3;
    private const short LargeRootYukon = 5;

    /// <summary>Reads the full payload referenced by a 16 byte LOB pointer.</summary>
    public static byte[]? TryRead(SqlDatabase database, ReadOnlySpan<byte> pointer)
    {
        if (pointer.Length != 16)
            return null;

        var root = new SlotPointer(pointer.Slice(8, 8));

        if (root.PageID <= 0)
            return null;

        try
        {
            using var output = new MemoryStream();
            Append(database, root, output, 0);
            return output.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static void Append(SqlDatabase database, SlotPointer slot, MemoryStream output, int depth)
    {
        if (depth > MaxDepth || output.Length > MaxTotalBytes)
            throw new InvalidDataException("LOB structure is too deep or too large.");

        var page = database.GetTextMixPage(slot.PagePointer);

        if (slot.SlotID < 0 || slot.SlotID >= page.Records.Length)
            throw new InvalidDataException($"LOB slot {slot} is outside the page slot array.");

        var record = page.Records[slot.SlotID];

        if (!record.IsValid || record.FixedLengthData.Length < 10)
            throw new InvalidDataException($"LOB slot {slot} does not contain a valid text record.");

        var data = record.FixedLengthData;
        short type = BitConverter.ToInt16(data, 8);

        switch (type)
        {
            case SmallRoot:
            {
                short length = BitConverter.ToInt16(data, 10);

                if (length > 0 && 16 + length <= data.Length)
                    output.Write(data, 16, length);
                else if (length > 0)
                    output.Write(data, 16, data.Length - 16);

                break;
            }

            case Data:
                output.Write(data, 10, data.Length - 10);
                break;

            case LargeRootYukon:
            {
                short links = BitConverter.ToInt16(data, 12);
                int offset = 20;

                for (int i = 0; i < links && offset + 12 <= data.Length; i++)
                {
                    var child = new SlotPointer(data.AsSpan(offset + 4, 8));
                    offset += 12;
                    Append(database, child, output, depth + 1);
                }

                break;
            }

            case Internal:
            {
                short links = BitConverter.ToInt16(data, 12);
                int offset = 16;

                for (int i = 0; i < links && offset + 16 <= data.Length; i++)
                {
                    var child = new SlotPointer(data.AsSpan(offset + 8, 8));
                    offset += 16;
                    Append(database, child, output, depth + 1);
                }

                break;
            }

            default:
                throw new InvalidDataException($"Unknown LOB structure type {type}.");
        }
    }
}
