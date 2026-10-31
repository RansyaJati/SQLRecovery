using System.IO.MemoryMappedFiles;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// A single SQL Server database file (MDF/NDF/LDF) opened as a memory mapped file so that
/// multi-gigabyte databases can be inspected without loading them into memory.
/// </summary>
public sealed class MdfFile : IDisposable
{
    public const int PageSize = 8192;

    private readonly FileStream _stream;
    private readonly MemoryMappedFile _mapped;
    private readonly MemoryMappedViewAccessor _view;
    private readonly object _sync = new();

    public string Path { get; }
    public long Length { get; }
    public int PageCount { get; }

    /// <summary>File id as declared by the file header page (page 0).</summary>
    public short FileID { get; internal set; } = 1;

    private MdfFile(string path, FileStream stream, MemoryMappedFile mapped, MemoryMappedViewAccessor view)
    {
        Path = path;
        _stream = stream;
        _mapped = mapped;
        _view = view;
        Length = stream.Length;
        PageCount = (int)(Length / PageSize);
    }

    public static MdfFile Open(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Database file not found.", path);

        // FileShare.ReadWrite so we can open files that are attached/copied elsewhere.
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var mapped = MemoryMappedFile.CreateFromFile(
            stream,
            mapName: null,
            capacity: 0,
            access: MemoryMappedFileAccess.Read,
            inheritability: HandleInheritability.None,
            leaveOpen: true);

        var view = mapped.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        return new MdfFile(path, stream, mapped, view);
    }

    public bool ContainsPage(int pageId) => pageId >= 0 && pageId < PageCount;

    /// <summary>Reads a full 8 KB page. Throws when the page id is outside the file.</summary>
    public byte[] ReadPage(int pageId)
    {
        if (!ContainsPage(pageId))
            throw new ArgumentOutOfRangeException(nameof(pageId),
                $"Page {pageId} is outside file '{Path}' (page count {PageCount}).");

        var buffer = new byte[PageSize];

        lock (_sync)
        {
            _view.ReadArray((long)pageId * PageSize, buffer, 0, PageSize);
        }

        return buffer;
    }

    /// <summary>Reads raw bytes at an arbitrary offset (used for LOB chains).</summary>
    public byte[] ReadBytes(long offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > Length)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var buffer = new byte[length];

        lock (_sync)
        {
            _view.ReadArray(offset, buffer, 0, length);
        }

        return buffer;
    }

    public readonly record struct PageRange(int StartPage, int PageCount);

    public void Dispose()
    {
        _view.Dispose();
        _mapped.Dispose();
        _stream.Dispose();
    }

    public override string ToString() => $"{System.IO.Path.GetFileName(Path)} (file {FileID}, {PageCount:N0} pages)";
}
