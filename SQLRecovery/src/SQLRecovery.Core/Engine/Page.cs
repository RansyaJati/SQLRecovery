namespace SQLRecovery.Core.Engine;

/// <summary>
/// A raw 8 KB database page together with its parsed header.
/// </summary>
public class Page
{
    public const int Size = 8192;

    public byte[] RawBytes { get; }
    public PageHeader Header { get; }
    public SqlDatabase Database { get; }

    public Page(byte[] bytes, SqlDatabase database)
    {
        if (bytes.Length != Size)
            throw new ArgumentException($"Page must be exactly {Size} bytes (was {bytes.Length}).", nameof(bytes));

        RawBytes = bytes;
        Database = database;
        Header = new PageHeader(bytes.AsSpan(0, PageHeader.Size));
    }

    /// <summary>The 8,096 bytes following the page header.</summary>
    public ReadOnlySpan<byte> Body => RawBytes.AsSpan(PageHeader.Size);

    public override string ToString() => $"{{{Header.Type} {Header.Pointer}}}";
}
