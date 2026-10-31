using SQLRecovery.Core.Engine;
using SQLRecovery.Core.Engine.Allocation;
using SQLRecovery.Core.Engine.Records;

namespace SQLRecovery.Core.Catalog;

/// <summary>
/// Reads table data either from a clustered index leaf chain or (for heaps) by following the
/// IAM chain and its extents.
/// </summary>
public sealed class TableDataReader
{
    private readonly SqlDatabase _database;

    public TableDataReader(SqlDatabase database) => _database = database;

    public IEnumerable<object?[]> ReadRows(Schema.TableSchema table, bool includeDeleted = false)
    {
        if (table.Columns.Count == 0)
            yield break;

        if (!table.IsHeap && !table.FirstPage.IsZero)
        {
            foreach (var row in ReadLinkedPages(table.FirstPage, table, includeDeleted))
                yield return row;

            yield break;
        }

        if (!table.FirstIamPage.IsZero)
        {
            foreach (var row in ReadHeap(table.FirstIamPage, table, includeDeleted))
                yield return row;

            yield break;
        }

        if (!table.FirstPage.IsZero)
        {
            foreach (var row in ReadLinkedPages(table.FirstPage, table, includeDeleted))
                yield return row;
        }
    }

    private IEnumerable<object?[]> ReadLinkedPages(PagePointer firstPage, Schema.TableSchema table, bool includeDeleted)
    {
        var pointer = firstPage;
        int guard = 0;

        while (!pointer.IsZero && guard++ < 5_000_000)
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

            foreach (var row in ReadPageRecords(page, table, includeDeleted))
                yield return row;

            pointer = page.Header.NextPage;
        }
    }

    private IEnumerable<object?[]> ReadHeap(PagePointer firstIamPage, Schema.TableSchema table, bool includeDeleted)
    {
        var visited = new HashSet<PagePointer>();
        var iamPointer = firstIamPage;
        int guard = 0;

        while (!iamPointer.IsZero && guard++ < 1_000_000)
        {
            IamPage iam;

            try
            {
                iam = _database.GetIamPage(iamPointer);
            }
            catch
            {
                yield break;
            }

            foreach (var slot in iam.Slots)
            {
                if (slot.IsZero || !visited.Add(slot))
                    continue;

                foreach (var row in ReadSinglePage(slot, table, includeDeleted))
                    yield return row;
            }

            foreach (var extent in iam.GetAllocatedExtents())
            {
                foreach (var pointer in extent.GetPagePointers())
                {
                    if (!visited.Add(pointer))
                        continue;

                    if (!_database.IsPageAllocated(pointer))
                        continue;

                    foreach (var row in ReadSinglePage(pointer, table, includeDeleted))
                        yield return row;
                }
            }

            iamPointer = iam.Header.NextPage;
        }
    }

    private IEnumerable<object?[]> ReadSinglePage(PagePointer pointer, Schema.TableSchema table, bool includeDeleted)
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

        if (page.Header.Type != PageType.Data && page.Header.Type != PageType.Index)
            yield break;

        if (_database.Version < SqlServerVersion.SqlServer2005 && page.Header.ObjectID != 0 && page.Header.ObjectID != table.ObjectId)
            yield break;

        foreach (var row in ReadPageRecords(page, table, includeDeleted))
            yield return row;
    }

    private static IEnumerable<object?[]> ReadPageRecords(PrimaryRecordPage page, Schema.TableSchema table, bool includeDeleted)
    {
        foreach (var record in page.Records)
        {
            if (!record.IsValid)
                continue;

            if (!includeDeleted && record.Type is RecordType.GhostData
                or RecordType.GhostIndex or RecordType.GhostVersion)
                continue;

            if (record.Type == RecordType.Index && page.Header.Level > 0)
                continue;

            object?[] values;

            try
            {
                values = TableRowReader.Read(table.Columns, record);
            }
            catch
            {
                continue;
            }

            yield return values;
        }
    }
}
