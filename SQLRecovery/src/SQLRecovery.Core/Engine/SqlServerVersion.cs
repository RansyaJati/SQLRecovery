namespace SQLRecovery.Core.Engine;

/// <summary>SQL Server database versions as recorded in the boot page.</summary>
public enum SqlServerVersion
{
    Unknown = 0,
    SqlServer7 = 515,
    SqlServer2000 = 539,
    SqlServer2005 = 611,
    SqlServer2008 = 655,
    SqlServer2008R2 = 661,
    SqlServer2012 = 706,
    SqlServer2014 = 782,
    SqlServer2016 = 852,
    SqlServer2017 = 868,
    SqlServer2019 = 895,
    SqlServer2022 = 957
}

public static class SqlServerVersionExtensions
{
    public static string ToDisplayName(this SqlServerVersion version) => version switch
    {
        SqlServerVersion.SqlServer7 => "SQL Server 7.0",
        SqlServerVersion.SqlServer2000 => "SQL Server 2000",
        SqlServerVersion.SqlServer2005 => "SQL Server 2005",
        SqlServerVersion.SqlServer2008 => "SQL Server 2008",
        SqlServerVersion.SqlServer2008R2 => "SQL Server 2008 R2",
        SqlServerVersion.SqlServer2012 => "SQL Server 2012",
        SqlServerVersion.SqlServer2014 => "SQL Server 2014",
        SqlServerVersion.SqlServer2016 => "SQL Server 2016",
        SqlServerVersion.SqlServer2017 => "SQL Server 2017",
        SqlServerVersion.SqlServer2019 => "SQL Server 2019",
        SqlServerVersion.SqlServer2022 => "SQL Server 2022",
        _ => "Unknown"
    };

    /// <summary>Maps a raw internal version number to the closest known release.</summary>
    public static SqlServerVersion FromInternalVersion(int version)
    {
        SqlServerVersion best = SqlServerVersion.Unknown;

        foreach (SqlServerVersion candidate in Enum.GetValues<SqlServerVersion>())
        {
            if ((int)candidate <= version && (int)candidate > (int)best)
                best = candidate;
        }

        return best;
    }
}
