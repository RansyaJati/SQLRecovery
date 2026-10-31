using System.Diagnostics;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// A reference to a single record (slot) on a page.
/// </summary>
[DebuggerDisplay("({FileID}:{PageID}:{SlotID})")]
public readonly struct SlotPointer : IEquatable<SlotPointer>
{
    public readonly short FileID;
    public readonly int PageID;
    public readonly short SlotID;

    public SlotPointer(short fileID, int pageID, short slotID)
    {
        FileID = fileID;
        PageID = pageID;
        SlotID = slotID;
    }

    /// <summary>Parses 8 bytes laid out as pageID(4) fileID(2) slotID(2).</summary>
    public SlotPointer(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8)
            throw new ArgumentException("SlotPointer requires 8 bytes.", nameof(bytes));

        PageID = BitConverter.ToInt32(bytes[..4]);
        FileID = BitConverter.ToInt16(bytes.Slice(4, 2));
        SlotID = BitConverter.ToInt16(bytes.Slice(6, 2));
    }

    public PagePointer PagePointer => new(FileID, PageID);

    public bool Equals(SlotPointer other) =>
        PageID == other.PageID && FileID == other.FileID && SlotID == other.SlotID;

    public override bool Equals(object? obj) => obj is SlotPointer other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(FileID, PageID, SlotID);

    public static bool operator ==(SlotPointer a, SlotPointer b) => a.Equals(b);

    public static bool operator !=(SlotPointer a, SlotPointer b) => !a.Equals(b);

    public override string ToString() => $"({FileID}:{PageID}:{SlotID})";
}
