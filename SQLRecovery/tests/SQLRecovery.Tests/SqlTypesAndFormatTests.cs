using System.Globalization;
using System.Text;
using SQLRecovery.Core.Schema;
using SQLRecovery.Core.SqlTypes;
using SQLRecovery.Export;
using Xunit;

namespace SQLRecovery.Tests;

public class SqlTypesAndFormatTests
{
    [Fact]
    public void PagePointer_ParsesCorrectly()
    {
        // 4 bytes page id (135), 2 bytes file id (1)
        byte[] bytes = [0x87, 0x00, 0x00, 0x00, 0x01, 0x00];
        var pointer = new Core.Engine.PagePointer(bytes);

        Assert.Equal(1, pointer.FileID);
        Assert.Equal(135, pointer.PageID);
        Assert.False(pointer.IsZero);
    }

    [Fact]
    public void DecimalType_CalculatesCorrectPrecisionAndScale()
    {
        var decType = new SqlDecimalType(12, 2);
        Assert.Equal((short?)9, decType.FixedLength);
        Assert.False(decType.IsVariableLength);

        // Layout: [sign (1), ints[0] (4), ints[1] (4)...]
        // Value: 1234.56 -> 123456 = 0x0001E240, positive sign = 1
        byte[] raw = new byte[9];
        raw[0] = 1; // positive
        raw[1] = 0x40;
        raw[2] = 0xE2;
        raw[3] = 0x01;
        raw[4] = 0x00;

        var val = decType.GetValue(raw);
        Assert.NotNull(val);
        Assert.Equal(1234.56m, (decimal)val);
    }

    [Fact]
    public void TemporalUtil_DecodesDateAndDateTimeCorrectly()
    {
        // 2026-10-30 = days since 0001-01-01
        var targetDate = new DateTime(2026, 10, 30);
        int days = (targetDate - new DateTime(1, 1, 1)).Days;
        byte[] dayBytes = [(byte)(days & 0xFF), (byte)((days >> 8) & 0xFF), (byte)((days >> 16) & 0xFF)];

        var decodedDate = TemporalUtil.DateFromDays(dayBytes);
        Assert.Equal(targetDate, decodedDate);
    }

    [Fact]
    public void ValueFormatter_HandlesSqlLiteralsAccurately()
    {
        // Standard strings
        Assert.Equal("N'O''Reilly'", ValueFormatter.ToSqlLiteral("O'Reilly"));
        
        // Null
        Assert.Equal("NULL", ValueFormatter.ToSqlLiteral(null));

        // Boolean
        Assert.Equal("1", ValueFormatter.ToSqlLiteral(true));
        Assert.Equal("0", ValueFormatter.ToSqlLiteral(false));

        // Binary
        byte[] bin = [0xDE, 0xAD, 0xBE, 0xEF];
        Assert.Equal("0xDEADBEEF", ValueFormatter.ToSqlLiteral(bin));

        // Non-unicode column with ASCII text doesn't need N prefix
        var varcharCol = new ColumnDefinition { Name = "Col", Type = SqlDbType.Varchar };
        Assert.Equal("'Hello World'", ValueFormatter.ToSqlLiteral("Hello World", varcharCol));

        // Unicode column gets N prefix
        var nvarcharCol = new ColumnDefinition { Name = "Col", Type = SqlDbType.NVarChar };
        Assert.Equal("N'Hello World'", ValueFormatter.ToSqlLiteral("Hello World", nvarcharCol));
    }

    [Fact]
    public void ValueFormatter_QuotesCsvFieldsWhenNecessary()
    {
        var options = new ExportOptions();

        // No quotes needed
        Assert.Equal("SimpleText", ValueFormatter.QuoteCsv("SimpleText", options));

        // Quotes needed due to delimiter
        Assert.Equal("\"Text,with,comma\"", ValueFormatter.QuoteCsv("Text,with,comma", options));

        // Quotes needed due to existing quote
        Assert.Equal("\"He said \"\"Hi\"\"\"", ValueFormatter.QuoteCsv("He said \"Hi\"", options));
    }

    [Fact]
    public void ColumnDefinition_SupportsSql2000Offsets()
    {
        var col = new ColumnDefinition
        {
            Name = "harga",
            Type = SqlDbType.Money,
            RecordOffset = 4
        };

        Assert.Equal(4, col.RecordOffset);
        Assert.False(col.IsVariableLength);
        Assert.Equal((short?)8, col.FixedLength);
    }
}
