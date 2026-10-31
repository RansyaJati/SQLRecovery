using SQLRecovery.Core.Engine;

namespace SQLRecovery.Core.Schema;

/// <summary>High level information about the opened database.</summary>
public sealed class DatabaseInfo
{
    public string Name { get; init; } = string.Empty;
    public string FilePath { get; init; } = string.Empty;
    public SqlServerVersion Version { get; init; }
    public int InternalVersion { get; init; }
    public short DatabaseId { get; init; }
    public long TotalPages { get; init; }
    public int FileCount { get; init; }
    public long FileSizeBytes { get; init; }
    public int TableCount { get; set; }

    public string VersionDisplay => Version.ToDisplayName();
}
