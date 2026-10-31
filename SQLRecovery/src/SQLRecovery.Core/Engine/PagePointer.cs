using System.Diagnostics;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// A reference to a page inside a database: file id + page id.
/// </summary>
[DebuggerDisplay("({FileID}:{PageID})")]
public readonly struct PagePointer : IEquatable<PagePointer>
{
    public readonly short FileID;
    public readonly int PageID;

    public static readonly PagePointer Zero = new(0, 0);

    public PagePointer(short fileID, int pageID)
    {
        FileID = fileID;
        PageID = pageID;
    }

    /// <summary>Parses 6 bytes laid out as pageID(4) fileID(2).</summary>
    public PagePointer(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 6)
            throw new ArgumentException("PagePointer requires 6 bytes.", nameof(bytes));

        PageID = BitConverter.ToInt32(bytes[..4]);
        FileID = BitConverter.ToInt16(bytes.Slice(4, 2));
    }

    public bool IsZero => PageID == 0 && FileID == 0;

    public bool Equals(PagePointer other) => PageID == other.PageID && FileID == other.FileID;

    public override bool Equals(object? obj) => obj is PagePointer other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(FileID, PageID);

    public static bool operator ==(PagePointer a, PagePointer b) => a.Equals(b);

    public static bool operator !=(PagePointer a, PagePointer b) => !a.Equals(b);

    public override string ToString() => $"({FileID}:{PageID})";
}
