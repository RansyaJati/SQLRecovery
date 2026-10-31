namespace SQLRecovery.Core.Engine;

/// <summary>
/// Page 0 of every database file: the file header. Carries the file id and the database
/// binding id.
/// </summary>
public sealed class FileHeaderPage : PrimaryRecordPage
{
    public Guid BindingID { get; private set; }
    public short FileID { get; private set; }

    public FileHeaderPage(byte[] bytes, SqlDatabase database) : base(bytes, database)
    {
        var record = Records.FirstOrDefault(r => r.IsValid);

        if (record is null)
            return;

        if (record.VariableLengthColumnData.TryGetValue(0, out var guidBytes) && guidBytes.Length == 16)
            BindingID = new Guid(guidBytes);

        if (record.VariableLengthColumnData.TryGetValue(2, out var fileIdBytes) && fileIdBytes.Length >= 2)
            FileID = BitConverter.ToInt16(fileIdBytes);
    }
}
