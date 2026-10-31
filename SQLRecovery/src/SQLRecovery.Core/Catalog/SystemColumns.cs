using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Core.Catalog;

/// <summary>Physical column layouts of the SQL Server 2005+ system base tables.</summary>
internal static class SystemColumns
{
    private static ColumnDefinition Col(string name, SqlDbType type, short maxLength = 0,
        byte precision = 0, byte scale = 0, bool nullable = false)
        => new()
        {
            Name = name,
            Type = type,
            MaxLength = maxLength,
            Precision = precision,
            Scale = scale,
            IsNullable = nullable
        };

    private static ColumnDefinition SysName(string name, bool nullable = false) =>
        Col(name, SqlDbType.NVarChar, 256, nullable: nullable);

    /// <summary>sys.sysallocunits (auid == object id for system tables).</summary>
    public static readonly ColumnDefinition[] AllocUnits =
    {
        Col("auid", SqlDbType.BigInt),
        Col("type", SqlDbType.TinyInt),
        Col("ownerid", SqlDbType.BigInt),
        Col("status", SqlDbType.Int),
        Col("fgid", SqlDbType.SmallInt),
        Col("pgfirst", SqlDbType.Binary, 6),
        Col("pgroot", SqlDbType.Binary, 6),
        Col("pgfirstiam", SqlDbType.Binary, 6),
        Col("pcused", SqlDbType.BigInt),
        Col("pcdata", SqlDbType.BigInt),
        Col("pcreserved", SqlDbType.BigInt),
        Col("dbfragid", SqlDbType.Int)
    };

    /// <summary>sys.sysrowsets: one row per allocation unit / index.</summary>
    public static readonly ColumnDefinition[] Rowsets =
    {
        Col("rowsetid", SqlDbType.BigInt),
        Col("ownertype", SqlDbType.TinyInt),
        Col("idmajor", SqlDbType.Int),
        Col("idminor", SqlDbType.Int),
        Col("numpart", SqlDbType.Int),
        Col("status", SqlDbType.Int),
        Col("fgidfs", SqlDbType.SmallInt),
        Col("rcrows", SqlDbType.BigInt),
        Col("cmprlevel", SqlDbType.TinyInt),
        Col("fillfact", SqlDbType.TinyInt),
        Col("maxnullbit", SqlDbType.SmallInt),
        Col("maxleaf", SqlDbType.Int),
        Col("maxint", SqlDbType.SmallInt),
        Col("minleaf", SqlDbType.SmallInt),
        Col("minint", SqlDbType.SmallInt),
        Col("rsguid", SqlDbType.VarBinary, -1, nullable: true),
        Col("lockres", SqlDbType.VarBinary, -1, nullable: true),
        Col("dbfragid", SqlDbType.Int)
    };

    /// <summary>sys.sysschobjs: schema-scoped objects (tables, views, ...).</summary>
    public static readonly ColumnDefinition[] Objects =
    {
        Col("id", SqlDbType.Int),
        SysName("name"),
        Col("nsid", SqlDbType.Int),
        Col("nsclass", SqlDbType.TinyInt),
        Col("status", SqlDbType.Int),
        Col("type", SqlDbType.Char, 2),
        Col("pid", SqlDbType.Int),
        Col("pclass", SqlDbType.TinyInt),
        Col("intprop", SqlDbType.Int),
        Col("created", SqlDbType.DateTime),
        Col("modified", SqlDbType.DateTime)
    };

    /// <summary>sys.syscolpars: columns and parameters.</summary>
    public static readonly ColumnDefinition[] Columns =
    {
        Col("id", SqlDbType.Int),
        Col("number", SqlDbType.SmallInt),
        Col("colid", SqlDbType.Int),
        SysName("name", nullable: true),
        Col("xtype", SqlDbType.TinyInt),
        Col("utype", SqlDbType.Int),
        Col("length", SqlDbType.SmallInt),
        Col("prec", SqlDbType.TinyInt),
        Col("scale", SqlDbType.TinyInt),
        Col("collationid", SqlDbType.Int),
        Col("status", SqlDbType.Int),
        Col("maxinrow", SqlDbType.SmallInt),
        Col("xmlns", SqlDbType.Int),
        Col("dflt", SqlDbType.Int),
        Col("chk", SqlDbType.Int),
        Col("idtval", SqlDbType.VarBinary, -1, nullable: true)
    };

    /// <summary>sys.sysidxstats: indexes.</summary>
    public static readonly ColumnDefinition[] Indexes =
    {
        Col("id", SqlDbType.Int),
        Col("indid", SqlDbType.Int),
        SysName("name", nullable: true),
        Col("status", SqlDbType.Int),
        Col("intprop", SqlDbType.Int),
        Col("fillfact", SqlDbType.TinyInt),
        Col("type", SqlDbType.TinyInt),
        Col("tinyprop", SqlDbType.TinyInt),
        Col("dataspace", SqlDbType.Int),
        Col("lobds", SqlDbType.Int),
        Col("rowset", SqlDbType.BigInt)
    };

    /// <summary>sys.sysscalartypes: scalar type definitions.</summary>
    public static readonly ColumnDefinition[] ScalarTypes =
    {
        Col("id", SqlDbType.Int),
        Col("schid", SqlDbType.Int),
        SysName("name"),
        Col("xtype", SqlDbType.TinyInt),
        Col("length", SqlDbType.SmallInt),
        Col("prec", SqlDbType.TinyInt),
        Col("scale", SqlDbType.TinyInt),
        Col("collationid", SqlDbType.Int),
        Col("status", SqlDbType.Int),
        Col("created", SqlDbType.DateTime),
        Col("modified", SqlDbType.DateTime),
        Col("dflt", SqlDbType.Int),
        Col("chk", SqlDbType.Int)
    };
}
