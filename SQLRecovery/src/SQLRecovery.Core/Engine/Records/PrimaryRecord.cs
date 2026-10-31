namespace SQLRecovery.Core.Engine.Records;

/// <summary>
/// Parses the standard SQL Server row format (status bits A/B, fixed data, null bitmap,
/// variable length columns). Row compression / sparse vectors are not decoded yet.
/// </summary>
public sealed class PrimaryRecord : Record
{
    public bool IsGhostForwardedRecord { get; private set; }

    public PrimaryRecord(ReadOnlyMemory<byte> bytes, Page page, int slotId, bool isValid) : base(page)
    {
        IsValid = isValid;

        if (!isValid || bytes.Length < 2)
            return;

        try
        {
            Parse(bytes.Span);
        }
        catch
        {
            IsValid = false;
        }
    }

    private void Parse(ReadOnlySpan<byte> bytes)
    {
        int offset = 0;

        ParseStatusBitsA(bytes[offset++]);

        if (Type == RecordType.ForwardingStub)
        {
            // The stub points at the real record: pageID(4) fileID(2) slotID(2).
            var target = new SlotPointer(bytes.Slice(offset, 8));
            FixedLengthData = bytes.Slice(offset, 8).ToArray();

            if (TryResolveForwarded(target, out var forwarded))
            {
                RawBytes = forwarded.RawBytes;
                FixedLengthData = forwarded.FixedLengthData;
                NumberOfColumns = forwarded.NumberOfColumns;
                NullBitmap = forwarded.NullBitmap;
                NumberOfVariableLengthColumns = forwarded.NumberOfVariableLengthColumns;
                HasNullBitmap = forwarded.HasNullBitmap;
                HasVariableLengthColumns = forwarded.HasVariableLengthColumns;
                foreach (var kv in forwarded.VariableLengthColumnData)
                    VariableLengthColumnData[kv.Key] = kv.Value;
                Type = RecordType.Forwarded;
            }

            IsValid = true;
            return;
        }

        if (Type == RecordType.Primary || Type == RecordType.Forwarded || Type == RecordType.Index)
        {
            // Forwarded records reuse the same layout as primary records.
        }

        // Status bits B (only meaningful for ghost forwarded records).
        byte statusB = bytes[offset++];
        IsGhostForwardedRecord = statusB == 1;

        // Fixed length size includes the two status bytes and this size field itself.
        int fixedLengthSize = BitConverter.ToInt16(bytes[offset..]) - 4;
        offset += 2;

        if (fixedLengthSize < 0)
        {
            IsValid = false;
            return;
        }

        FixedLengthData = bytes.Slice(offset, fixedLengthSize).ToArray();
        offset += fixedLengthSize;

        NumberOfColumns = BitConverter.ToInt16(bytes[offset..]);
        offset += 2;

        if (NumberOfColumns < 0)
        {
            IsValid = false;
            return;
        }

        if (HasNullBitmap)
            ParseNullBitmap(bytes, ref offset);

        if (HasVariableLengthColumns)
            ParseVariableLengthColumns(bytes, ref offset);

        RawBytes = bytes[..Math.Min(offset, bytes.Length)].ToArray();
        IsValid = true;
    }

    private bool TryResolveForwarded(SlotPointer target, out PrimaryRecord forwarded)
    {
        forwarded = null!;

        if (Page.Database is null || target.PageID <= 0)
            return false;

        try
        {
            var forwardPage = Page.Database.GetPrimaryRecordPage(target.PagePointer);

            if (target.SlotID < 0 || target.SlotID >= forwardPage.Records.Length)
                return false;

            var candidate = forwardPage.Records[target.SlotID];

            if (!candidate.IsValid || candidate.Type == RecordType.ForwardingStub)
                return false;

            forwarded = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString() =>
        $"Record(Type={Type}, Columns={NumberOfColumns}, Fixed={FixedLengthData.Length}, Var={VariableLengthColumnData.Count})";
}
