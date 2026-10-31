namespace SQLRecovery.Core.Engine.Allocation;

public sealed class GamPage : ExtentAllocationMap
{
    public GamPage(byte[] bytes, SqlDatabase database) : base(bytes, database) { }
}

public sealed class SgamPage : ExtentAllocationMap
{
    public SgamPage(byte[] bytes, SqlDatabase database) : base(bytes, database) { }
}

/// <summary>An 8-page extent starting at <see cref="StartPage"/>.</summary>
public sealed class ExtentPointer
{
    public PagePointer StartPage { get; }
    public PagePointer EndPage { get; }

    public ExtentPointer(PagePointer start)
    {
        StartPage = start;
        EndPage = new PagePointer(start.FileID, start.PageID + 7);
    }

    public IEnumerable<PagePointer> GetPagePointers()
    {
        for (int pageId = StartPage.PageID; pageId <= EndPage.PageID; pageId++)
            yield return new PagePointer(StartPage.FileID, pageId);
    }

    public override string ToString() => $"{StartPage} - {EndPage}";
}
