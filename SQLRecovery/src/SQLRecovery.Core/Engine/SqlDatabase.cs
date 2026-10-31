using SQLRecovery.Core.Engine.Records;

namespace SQLRecovery.Core.Engine;

/// <summary>
/// Represents an opened SQL Server database (one or more MDF/NDF files) and provides cached
/// access to its pages.
/// </summary>
public sealed class SqlDatabase : IDisposable
{
    private const int MaxCachedPages = 8192;

    private readonly Dictionary<short, MdfFile> _files = new();
    private readonly Dictionary<PagePointer, Page> _pageCache = new();
    private readonly object _sync = new();

    public string Name { get; private set; } = "(unknown)";
    public SqlServerVersion Version { get; private set; }
    public int InternalVersion { get; private set; }
    public short DatabaseId { get; private set; }
    public BootPage? Boot { get; private set; }

    public IReadOnlyList<MdfFile> DataFiles { get; private set; } = Array.Empty<MdfFile>();
    public IReadOnlyList<MdfFile> LogFiles { get; private set; } = Array.Empty<MdfFile>();
    public IEnumerable<MdfFile> AllFiles => DataFiles.Concat(LogFiles);

    public long TotalPages => DataFiles.Sum(f => (long)f.PageCount);

    private SqlDatabase()
    {
    }

    public static SqlDatabase Open(string path) => Open(new[] { path });

    /// <summary>Opens a database from one or more files (primary MDF plus optional NDF/LDF).</summary>
    public static SqlDatabase Open(IEnumerable<string> paths)
    {
        var all = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (all.Count == 0)
            throw new ArgumentException("At least one database file is required.", nameof(paths));

        var database = new SqlDatabase();
        var opened = new List<MdfFile>();

        try
        {
            foreach (var path in all)
                opened.Add(MdfFile.Open(path));

            var dataFiles = new List<MdfFile>();
            var logFiles = new List<MdfFile>();

            foreach (var file in opened)
            {
                bool isLog = string.Equals(Path.GetExtension(file.Path), ".ldf", StringComparison.OrdinalIgnoreCase);

                if (isLog)
                {
                    logFiles.Add(file);
                    continue;
                }

                // Resolve the real file id from the file header page (page 0).
                if (file.PageCount > 0)
                {
                    try
                    {
                        var header = new FileHeaderPage(file.ReadPage(0), database);
                        if (header.FileID > 0)
                            file.FileID = header.FileID;
                    }
                    catch
                    {
                        // Keep default file id; a later page may still be readable.
                    }
                }

                dataFiles.Add(file);
            }

            if (dataFiles.Count == 0)
                throw new InvalidDataException("No MDF/NDF data file was provided (only log files were found).");

            // Register data files by id. Duplicate ids are resolved by the first match.
            foreach (var file in dataFiles)
            {
                if (!database._files.ContainsKey(file.FileID))
                    database._files[file.FileID] = file;
            }

            // Ensure file id 1 exists so lookups never fail on odd files.
            if (!database._files.ContainsKey(1))
                database._files[1] = dataFiles[0];

            database.DataFiles = dataFiles;
            database.LogFiles = logFiles;

            database.ReadBootPage(all);

            return database;
        }
        catch
        {
            foreach (var file in opened)
                file.Dispose();
            throw;
        }
    }

    private void ReadBootPage(IEnumerable<string> requestedPaths)
    {
        var candidates = new List<MdfFile>();

        if (_files.TryGetValue(1, out var primary))
            candidates.Add(primary);

        candidates.AddRange(DataFiles.Where(f => f != primary));

        foreach (var file in candidates)
        {
            if (file.PageCount <= 9)
                continue;

            try
            {
                Boot = new BootPage(file.ReadPage(9), this);
            }
            catch
            {
                continue;
            }

            if (Boot is null || Boot.InternalVersion == 0)
                continue;

            InternalVersion = Boot.InternalVersion;
            Version = SqlServerVersionExtensions.FromInternalVersion(Boot.InternalVersion);
            DatabaseId = Boot.DBID;
            Name = string.IsNullOrWhiteSpace(Boot.DatabaseName) ? "(unnamed)" : Boot.DatabaseName;
            return;
        }

        Name = Path.GetFileNameWithoutExtension(Path.GetFileName(requestedPaths.FirstOrDefault() ?? "database"));
    }

    public MdfFile GetFile(short fileId)
    {
        if (_files.TryGetValue(fileId, out var file))
            return file;

        if (DataFiles.Count == 1)
            return DataFiles[0];

        throw new InvalidDataException($"Database file id {fileId} is not available. Provide the matching NDF file.");
    }

    public Page GetPage(PagePointer pointer) => GetPageInternal(pointer, isRecordPage: false);

    public PrimaryRecordPage GetPrimaryRecordPage(PagePointer pointer) =>
        (PrimaryRecordPage)GetPageInternal(pointer, isRecordPage: true);

    private Page GetPageInternal(PagePointer pointer, bool isRecordPage)
    {
        lock (_sync)
        {
            if (_pageCache.TryGetValue(pointer, out var cached))
                return cached;
        }

        var file = GetFile(pointer.FileID);

        if (!file.ContainsPage(pointer.PageID))
            throw new ArgumentOutOfRangeException(nameof(pointer),
                $"Page {pointer} does not exist in file {file.FileID}.");

        var bytes = file.ReadPage(pointer.PageID);
        Page page = isRecordPage ? new PrimaryRecordPage(bytes, this) : new Page(bytes, this);

        lock (_sync)
        {
            if (_pageCache.Count >= MaxCachedPages)
                _pageCache.Clear();

            _pageCache[pointer] = page;
        }

        return page;
    }

    /// <summary>Enumerates every page of every data file (slow for large databases).</summary>
    public IEnumerable<PagePointer> EnumerateAllPages()
    {
        foreach (var file in DataFiles)
            for (int pageId = 0; pageId < file.PageCount; pageId++)
                yield return new PagePointer(file.FileID, pageId);
    }

    /// <summary>Enumerates pages of all data files that match the requested types.</summary>
    public IEnumerable<PagePointer> EnumeratePages(params PageType[] types)
    {
        var wanted = types.Length == 0 ? null : new HashSet<PageType>(types);

        foreach (var pointer in EnumerateAllPages())
        {
            Page page;
            try
            {
                page = GetPage(pointer);
            }
            catch
            {
                continue;
            }

            if (wanted is null || wanted.Contains(page.Header.Type))
                yield return pointer;
        }
    }

    // --- Allocation helpers -------------------------------------------------

    public Allocation.IamPage GetIamPage(PagePointer pointer) =>
        new(GetFile(pointer.FileID).ReadPage(pointer.PageID), this);

    public TextMixPage GetTextMixPage(PagePointer pointer) =>
        new(GetFile(pointer.FileID).ReadPage(pointer.PageID), this);

    public Allocation.GamPage GetGamPage(PagePointer pointer) =>
        new(GetFile(pointer.FileID).ReadPage(pointer.PageID), this);

    public Allocation.SgamPage GetSgamPage(PagePointer pointer) =>
        new(GetFile(pointer.FileID).ReadPage(pointer.PageID), this);

    public Allocation.PfsPage GetPfsPage(PagePointer pointer) =>
        new(GetFile(pointer.FileID).ReadPage(pointer.PageID), this);

    /// <summary>Checks the PFS page that covers <paramref name="pointer"/>.</summary>
    public bool IsPageAllocated(PagePointer pointer)
    {
        try
        {
            var pfsPointer = Allocation.PfsPage.GetPfsPointerForPage(pointer);
            var pfs = GetPfsPage(pfsPointer);
            return pfs.IsAllocated(pointer.PageID);
        }
        catch
        {
            // If PFS can't be read assume allocated so recovery still surfaces data.
            return true;
        }
    }

    public void Dispose()
    {
        foreach (var file in AllFiles)
            file.Dispose();
    }

    public override string ToString() => $"{Name} ({Version.ToDisplayName()}, {DataFiles.Count} file(s), {TotalPages:N0} pages)";
}
