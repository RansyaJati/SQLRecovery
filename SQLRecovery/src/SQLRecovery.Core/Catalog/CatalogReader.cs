using System.Text;
using SQLRecovery.Core.Engine;
using SQLRecovery.Core.Engine.Records;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Core.Catalog;

public sealed record SysAllocUnit(long Auid, long OwnerId, byte Type, short Fgid,
    PagePointer FirstPage, PagePointer RootPage, PagePointer FirstIamPage);

public sealed record SysRowset(long RowsetId, int IdMajor, int IdMinor, long RowCount);

public sealed record SysObject(int Id, string Name, int Nsid, string Type);

public sealed record SysColumn(int Id, short Number, int ColId, string Name, byte XType,
    int UType, short Length, byte Prec, byte Scale, int CollationId, int Status, short Offset = 0);

public sealed record SysIndex(int Id, int IndId, string Name, byte Type, long Rowset);

/// <summary>
/// Bootstraps the SQL Server system catalog from the raw database files and exposes
/// table/column/index metadata for SQL Server 2000 through SQL Server 2022.
/// </summary>
public sealed class CatalogReader
{
    /// <summary>Fixed allocation unit id of sys.sysrowsets.</summary>
    private const long SysRowsetsAllocationUnit = 327680;

    private const int SysRowsetsObjectId = 5;
    private const int SysAllocUnitsObjectId = 7;
    private const int SysSchobjsObjectId = 34;
    private const int SysColparsObjectId = 41;
    private const int SysIdxStatsObjectId = 54;

    private readonly SqlDatabase _database;
    private readonly SystemTableScanner _scanner;

    public IReadOnlyList<SysAllocUnit> AllocationUnits { get; private set; } = Array.Empty<SysAllocUnit>();
    public IReadOnlyList<SysRowset> Rowsets { get; private set; } = Array.Empty<SysRowset>();
    public IReadOnlyList<SysObject> Objects { get; private set; } = Array.Empty<SysObject>();
    public IReadOnlyList<SysColumn> Columns { get; private set; } = Array.Empty<SysColumn>();
    public IReadOnlyList<SysIndex> Indexes { get; private set; } = Array.Empty<SysIndex>();

    public bool IsAvailable { get; private set; }
    public string? Error { get; private set; }
    public bool IsSql2000 => _database.Version < SqlServerVersion.SqlServer2005;

    public CatalogReader(SqlDatabase database)
    {
        _database = database;
        _scanner = new SystemTableScanner(database);

        try
        {
            if (IsSql2000)
                LoadSql2000();
            else
                Load();

            IsAvailable = Objects.Count > 0;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            IsAvailable = false;
        }
    }

    private void Load()
    {
        if (_database.Boot is null)
            throw new InvalidDataException("Boot page could not be read.");

        AllocationUnits = _scanner
            .Scan(_database.Boot.FirstSysIndexes, SystemColumns.AllocUnits)
            .Select(MapAllocUnit)
            .ToList();

        var rowsetsAu = AllocationUnits.FirstOrDefault(a => a.Auid == SysRowsetsAllocationUnit)
            ?? new SysAllocUnit(SysRowsetsAllocationUnit, 0, 1, 0, PagePointer.Zero, PagePointer.Zero, PagePointer.Zero);

        if (rowsetsAu.FirstPage.IsZero)
            throw new InvalidDataException("Could not locate sys.sysrowsets.");

        Rowsets = _scanner.Scan(rowsetsAu.FirstPage, SystemColumns.Rowsets)
            .Select(MapRowset)
            .ToList();

        Objects = ScanObjectTable(SysSchobjsObjectId, SystemColumns.Objects)
            .Select(MapObject)
            .ToList();

        Columns = ScanObjectTable(SysColparsObjectId, SystemColumns.Columns)
            .Select(MapColumn)
            .ToList();

        Indexes = ScanObjectTable(SysIdxStatsObjectId, SystemColumns.Indexes)
            .Select(MapIndex)
            .ToList();
    }

    private IEnumerable<object?[]> ScanObjectTable(int objectId, IReadOnlyList<ColumnDefinition> columns)
    {
        var firstPage = FindFirstPage(objectId);

        if (firstPage.IsZero)
            return Array.Empty<object?[]>();

        return _scanner.Scan(firstPage, columns);
    }

    private PagePointer FindFirstPage(int objectId)
    {
        var rowset = Rowsets.FirstOrDefault(r => r.IdMajor == objectId && r.IdMinor == 1);

        if (rowset is not null)
        {
            var au = AllocationUnits.FirstOrDefault(a => a.OwnerId == rowset.RowsetId && a.Type == 1)
                ?? AllocationUnits.FirstOrDefault(a => a.Auid == rowset.RowsetId && a.Type == 1);
            if (au is not null)
                return au.FirstPage;
        }

        // For system tables the allocation unit id equals the object id.
        var direct = AllocationUnits.FirstOrDefault(a => a.Auid == objectId && a.Type == 1)
            ?? AllocationUnits.FirstOrDefault(a => a.OwnerId == objectId && a.Type == 1);
        return direct?.FirstPage ?? PagePointer.Zero;
    }

    // --- mapping -----------------------------------------------------------

    private static SysAllocUnit MapAllocUnit(object?[] r) => new(
        AsLong(r, 0), AsLong(r, 2), AsByte(r, 1), AsShort(r, 4),
        AsPagePointer(r, 5), AsPagePointer(r, 6), AsPagePointer(r, 7));

    private static SysRowset MapRowset(object?[] r) => new(
        AsLong(r, 0), AsInt(r, 2), AsInt(r, 3), AsLong(r, 7));

    private static SysObject MapObject(object?[] r) => new(
        AsInt(r, 0), AsString(r, 1), AsInt(r, 2), AsString(r, 5));

    private static SysColumn MapColumn(object?[] r) => new(
        AsInt(r, 0), AsShort(r, 1), AsInt(r, 2), AsString(r, 3), AsByte(r, 4),
        AsInt(r, 5), AsShort(r, 6), AsByte(r, 7), AsByte(r, 8), AsInt(r, 9), AsInt(r, 10));

    private static SysIndex MapIndex(object?[] r) => new(
        AsInt(r, 0), AsInt(r, 1), AsString(r, 2), AsByte(r, 6), AsLong(r, 10));

    // --- SQL Server 2000 Catalog Loader -----------------------------------

    private void LoadSql2000()
    {
        var firstSysIndexes = _database.Boot?.FirstSysIndexes ?? PagePointer.Zero;
        if (firstSysIndexes.IsZero)
            firstSysIndexes = new PagePointer(1, 24);

        // 1. Scan sysindexes (objId = 2)
        var rawIndexes = new List<(int Id, short IndId, PagePointer First, PagePointer Root, PagePointer FirstIam, long RowCount, string Name)>();
        PagePointer syscolumnsFirstPage = PagePointer.Zero;

        foreach (var (_, record) in ScanPageChain(firstSysIndexes))
        {
            if (record.FixedLengthData.Length < 44)
                continue;

            int id = BitConverter.ToInt32(record.FixedLengthData, 0);
            short indid = BitConverter.ToInt16(record.FixedLengthData, 14);
            var first = new PagePointer(BitConverter.ToInt16(record.FixedLengthData, 12), BitConverter.ToInt32(record.FixedLengthData, 8));
            var root = new PagePointer(BitConverter.ToInt16(record.FixedLengthData, 20), BitConverter.ToInt32(record.FixedLengthData, 16));
            long rowCount = BitConverter.ToInt64(record.FixedLengthData, 40);
            if (rowCount < 0)
                rowCount = BitConverter.ToInt32(record.FixedLengthData, 40);

            var firstIam = record.FixedLengthData.Length >= 70
                ? new PagePointer(BitConverter.ToInt16(record.FixedLengthData, 68), BitConverter.ToInt32(record.FixedLengthData, 64))
                : PagePointer.Zero;

            string name = record.VariableLengthColumnData.TryGetValue(0, out var nameBytes)
                ? CleanSql2000Name(nameBytes)
                : string.Empty;

            rawIndexes.Add((id, indid, first, root, firstIam, rowCount, name));

            if (id == 3 && (indid == 1 || indid == 0) && !first.IsZero && syscolumnsFirstPage.IsZero)
                syscolumnsFirstPage = first;
        }

        if (syscolumnsFirstPage.IsZero)
            syscolumnsFirstPage = new PagePointer(1, 16);

        // 2. Scan sysobjects (objId = 1)
        var sysobjectsFirstPage = new PagePointer(1, 8);
        var objects = new List<SysObject>();

        foreach (var (_, record) in ScanPageChain(sysobjectsFirstPage))
        {
            if (record.FixedLengthData.Length < 10)
                continue;

            int id = BitConverter.ToInt32(record.FixedLengthData, 0);
            string xtype = Encoding.ASCII.GetString(record.FixedLengthData, 4, 2).Trim();
            short uid = BitConverter.ToInt16(record.FixedLengthData, 8);

            string name = record.VariableLengthColumnData.TryGetValue(0, out var nameBytes)
                ? CleanSql2000Name(nameBytes)
                : $"object_{id}";

            if (string.IsNullOrWhiteSpace(name))
                name = $"object_{id}";

            objects.Add(new SysObject(id, name, uid, xtype));
        }

        // 3. Scan syscolumns (objId = 3)
        var columns = new List<SysColumn>();

        foreach (var (_, record) in ScanPageChain(syscolumnsFirstPage))
        {
            if (record.FixedLengthData.Length < 16)
                continue;

            int tableId = BitConverter.ToInt32(record.FixedLengthData, 0);
            byte xtype = record.FixedLengthData[4];
            byte status = record.FixedLengthData.Length > 5 ? record.FixedLengthData[5] : (byte)0;
            short utype = BitConverter.ToInt16(record.FixedLengthData, 6);
            short length = BitConverter.ToInt16(record.FixedLengthData, 8);
            byte prec = record.FixedLengthData[10];
            byte scale = record.FixedLengthData[11];
            short colid = BitConverter.ToInt16(record.FixedLengthData, 12);
            short offset = BitConverter.ToInt16(record.FixedLengthData, 14);

            string name = record.VariableLengthColumnData.TryGetValue(0, out var nameBytes)
                ? CleanSql2000Name(nameBytes)
                : $"col_{colid}";

            if (string.IsNullOrWhiteSpace(name))
                name = $"col_{colid}";

            columns.Add(new SysColumn(tableId, 0, colid, name, xtype, utype, length, prec, scale, 0, status, offset));
        }

        // 4. Construct AllocationUnits, Rowsets, Indexes
        var allocUnits = new List<SysAllocUnit>();
        var rowsets = new List<SysRowset>();
        var indexes = new List<SysIndex>();

        foreach (var idx in rawIndexes)
        {
            long rowsetId = (((long)idx.Id) << 32) | (long)(ushort)idx.IndId;
            indexes.Add(new SysIndex(idx.Id, idx.IndId, idx.Name, idx.IndId == 1 ? (byte)1 : (byte)0, rowsetId));
            rowsets.Add(new SysRowset(rowsetId, idx.Id, idx.IndId, idx.RowCount));
            allocUnits.Add(new SysAllocUnit(rowsetId, rowsetId, 1, 1, idx.First, idx.Root, idx.FirstIam));
        }

        AllocationUnits = allocUnits;
        Rowsets = rowsets;
        Objects = objects;
        Columns = columns;
        Indexes = indexes;
    }

    private IEnumerable<(PrimaryRecordPage Page, PrimaryRecord Record)> ScanPageChain(PagePointer startPage)
    {
        var visited = new HashSet<PagePointer>();
        var pointer = startPage;
        int guard = 0;

        while (!pointer.IsZero && guard++ < 5_000_000 && visited.Add(pointer))
        {
            PrimaryRecordPage page;
            try
            {
                page = _database.GetPrimaryRecordPage(pointer);
            }
            catch
            {
                yield break;
            }

            foreach (var record in page.Records)
            {
                if (!record.IsValid)
                    continue;

                yield return (page, record);
            }

            pointer = page.Header.NextPage;
        }
    }

    private static string CleanSql2000Name(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        var str = Encoding.Unicode.GetString(bytes).TrimEnd('\0', ' ');
        int nul = str.IndexOf('\0');
        if (nul >= 0)
            str = str[..nul];

        var sb = new StringBuilder(str.Length);
        foreach (char c in str)
        {
            // Sanitize character high-bit quirk in SQL Server 2000 names (e.g. 0x016C 'Ŭ' -> 0x6C 'l')
            if (c >= 0x0100 && c <= 0x017F && char.IsLetterOrDigit((char)(c & 0x7F)))
                sb.Append((char)(c & 0x7F));
            else
                sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    // --- typed accessors ---------------------------------------------------

    private static long AsLong(object?[] r, int i) => r[i] switch
    {
        long l => l,
        int n => n,
        short s => s,
        byte b => b,
        decimal d => (long)d,
        _ => 0
    };

    private static int AsInt(object?[] r, int i) => (int)AsLong(r, i);

    private static short AsShort(object?[] r, int i) => (short)AsLong(r, i);

    private static byte AsByte(object?[] r, int i) => (byte)AsLong(r, i);

    private static string AsString(object?[] r, int i) => r[i] as string ?? string.Empty;

    private static PagePointer AsPagePointer(object?[] r, int i) =>
        r[i] is byte[] bytes && bytes.Length >= 6 ? new PagePointer(bytes) : PagePointer.Zero;
}
