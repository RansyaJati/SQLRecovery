using SQLRecovery.Core.Engine;
using SQLRecovery.Core.Schema;

namespace SQLRecovery.Core.Catalog;

/// <summary>
/// Reads rows from a system base table by walking a chain of linked data pages.
/// </summary>
internal sealed class SystemTableScanner
{
    private readonly SqlDatabase _database;

    public SystemTableScanner(SqlDatabase database) => _database = database;

    public IEnumerable<object?[]> Scan(PagePointer firstPage, IReadOnlyList<ColumnDefinition> columns)
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

            foreach (var record in page.Records)
            {
                if (!record.IsValid)
                    continue;

                yield return TableRowReader.Read(columns, record);
            }

            pointer = page.Header.NextPage;
        }
    }
}
