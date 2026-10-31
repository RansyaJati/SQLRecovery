using System.Text;
using SQLRecovery.Core.SqlTypes;

namespace SQLRecovery.Core.Engine;


/// <summary>
/// Converts a parsed record into an array of .NET values using a physical column layout.
/// Handles null bitmap, bit-packing and variable length sections.
/// </summary>
public static class TableRowReader
{
    public static object?[] Read(IReadOnlyList<Schema.ColumnDefinition> columns, Records.Record record)
    {
        var values = new object?[columns.Count];

        if (!record.IsValid)
            return values;

        var compiled = new ISqlType[columns.Count];
        for (int i = 0; i < columns.Count; i++)
            compiled[i] = SqlTypeFactory.Create(columns[i]);

        var fixedData = record.FixedLengthData;
        int fixedCursor = 0;
        int varIndex = 0;

        bool needBitByte = true;
        byte bitByte = 0;
        int bitPos = 0;

        for (int i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            // Computed columns occupy no storage in the row.
            if (column.IsComputed)
                continue;

            bool isNull = record.IsNull(i);

            if (column.Type == SqlDbType.Bit)
            {
                if (column.RecordOffset >= 4 && column.RecordOffset - 4 < fixedData.Length)
                {
                    int byteIdx = column.RecordOffset - 4;
                    int bPos = column.Precision < 8 ? column.Precision : 0;
                    bool bit = (fixedData[byteIdx] & (1 << bPos)) != 0;
                    values[i] = isNull ? null : bit;
                    continue;
                }

                if (needBitByte)
                {
                    if (fixedCursor >= fixedData.Length)
                        continue;

                    bitByte = fixedData[fixedCursor++];
                    bitPos = 0;
                    needBitByte = false;
                }

                bool bitVal = (bitByte & (1 << bitPos)) != 0;
                bitPos++;

                if (bitPos == 8)
                    needBitByte = true;

                values[i] = isNull ? null : bitVal;
                continue;
            }

            // Any non-bit column terminates the current bit run.
            needBitByte = true;

            if (column.IsVariableLength)
            {
                int ordinal = column.RecordOffset < 0
                    ? (-column.RecordOffset) - 1
                    : varIndex++;

                if (isNull)
                {
                    values[i] = null;
                    continue;
                }

                byte[] raw = record.VariableLengthColumnData.TryGetValue(ordinal, out var data)
                    ? data
                    : Array.Empty<byte>();

                if (record.ComplexColumns.Contains(ordinal) && raw.Length == 16)
                {
                    var lob = LobReader.TryRead(record.Page.Database, raw);

                    values[i] = lob is not null
                        ? DecodeLob(column, lob)
                        : new LobPointer(raw);

                    continue;
                }

                values[i] = raw.Length == 0 ? EmptyValueFor(column) : compiled[i].GetValue(raw);
                continue;
            }

            short length = column.FixedLength ?? (column.MaxLength > 0 ? column.MaxLength : (short)0);

            if (isNull)
            {
                if (column.RecordOffset == 0)
                    fixedCursor += length;
                values[i] = null;
                continue;
            }

            int byteOffset;
            if (column.RecordOffset >= 4)
            {
                byteOffset = column.RecordOffset - 4;
            }
            else
            {
                byteOffset = fixedCursor;
                fixedCursor += length;
            }

            if (length <= 0 || byteOffset + length > fixedData.Length)
            {
                values[i] = null;
                continue;
            }

            var slice = new byte[length];
            Array.Copy(fixedData, byteOffset, slice, 0, length);

            try
            {
                values[i] = compiled[i].GetValue(slice);
            }
            catch
            {
                values[i] = null;
            }
        }

        return values;
    }

    private static object? EmptyValueFor(Schema.ColumnDefinition column) => column.Type switch
    {
        SqlDbType.Varchar or SqlDbType.NVarChar or SqlDbType.Char or SqlDbType.NChar => string.Empty,
        SqlDbType.VarBinary or SqlDbType.Binary or SqlDbType.Image => Array.Empty<byte>(),
        SqlDbType.Xml => string.Empty,
        _ => null
    };

    /// <summary>Turns raw LOB bytes into the .NET value appropriate for the column type.</summary>
    private static object DecodeLob(Schema.ColumnDefinition column, byte[] bytes) => column.Type switch
    {
        SqlDbType.NVarChar or SqlDbType.NText or SqlDbType.Xml => Encoding.Unicode.GetString(bytes),
        SqlDbType.Varchar or SqlDbType.Char or SqlDbType.NChar or SqlDbType.Text =>
            CollationHelper.GetEncoding(column.CollationId).GetString(bytes),
        _ => bytes
    };
}
