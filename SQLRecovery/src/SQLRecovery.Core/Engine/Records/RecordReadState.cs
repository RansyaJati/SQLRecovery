namespace SQLRecovery.Core.Engine.Records;

/// <summary>
/// Tracks bit-level reads for values that share a single byte (for example <c>bit</c> columns).
/// </summary>
public sealed class RecordReadState
{
    private int _currentBitIndex = 8;
    private byte _bits;

    public void LoadBitByte(byte bits)
    {
        _bits = bits;
        _currentBitIndex = 0;
    }

    public bool AllBitsConsumed => _currentBitIndex == 8;

    public bool GetNextBit() => (_bits & (1 << _currentBitIndex++)) != 0;
}
