namespace SolSharp.Core.SysvarStates;

/// <summary>The result of checking a slot against the bounded slot-history window.</summary>
public enum SlotHistoryCheck
{
    /// <summary>The slot is newer than the history's newest slot.</summary>
    Future,

    /// <summary>The slot predates the retained history window.</summary>
    TooOld,

    /// <summary>The slot is marked present.</summary>
    Found,

    /// <summary>The slot is inside the retained window but is not marked present.</summary>
    NotFound
}

/// <summary>The exact fixed-size wincode state of the slot-history sysvar.</summary>
public sealed record SlotHistorySysvarState
{
    /// <summary>The fixed number of slot bits retained by the runtime.</summary>
    public const ulong MaximumEntries = 1_048_576;

    /// <summary>The exact serialized account-data length.</summary>
    public const int DataLength = 131_097;

    /// <summary>The exact number of serialized 64-bit bit-vector blocks.</summary>
    public const int BlockCount = 16_384;
    private const int BitsPerBlock = 64;
    private readonly ulong[] _blocks;

    private SlotHistorySysvarState(ulong[] blocks, ulong nextSlot)
    {
        _blocks = blocks;
        NextSlot = nextSlot;
    }

    /// <summary>The slot immediately after the newest recorded slot.</summary>
    public ulong NextSlot { get; }

    /// <summary>The oldest slot that can still be represented by the history window.</summary>
    public ulong OldestSlot => NextSlot >= MaximumEntries ? NextSlot - MaximumEntries : 0;

    /// <summary>The newest slot represented by the history.</summary>
    public ulong NewestSlot => unchecked(NextSlot - 1);

    /// <summary>Decodes the pinned canonical wincode representation of a slot-history account.</summary>
    /// <param name="data">The complete 131,097-byte account data.</param>
    /// <returns>The decoded fixed-size slot history.</returns>
    /// <exception cref="ArgumentException">
    /// The data length, bit-vector option tag, block count, or declared bit length is not canonical.
    /// </exception>
    public static SlotHistorySysvarState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length != DataLength)
            throw new ArgumentException($"Slot history data must be exactly {DataLength} bytes.", nameof(data));

        var reader = new SysvarBincodeReader(data);
        if (reader.ReadByte() is not 1)
            throw new ArgumentException("Slot history must contain the fixed bit-vector block allocation.", nameof(data));

        var blockCount = reader.ReadUInt64();
        if (blockCount != BlockCount)
            throw new ArgumentException($"Slot history must contain exactly {BlockCount} blocks.", nameof(data));

        var blocks = new ulong[BlockCount];
        for (var i = 0; i < blocks.Length; i++)
            blocks[i] = reader.ReadUInt64();

        var bitLength = reader.ReadUInt64();
        if (bitLength != MaximumEntries)
            throw new ArgumentException($"Slot history must declare exactly {MaximumEntries} bits.", nameof(data));

        var state = new SlotHistorySysvarState(blocks, reader.ReadUInt64());
        reader.EnsureEnd();
        return state;
    }

    /// <summary>Checks a slot using the pinned runtime's future, age-window, and bit-presence ordering.</summary>
    /// <param name="slot">The slot to check.</param>
    /// <returns>The slot's relationship to the retained history.</returns>
    public SlotHistoryCheck Check(ulong slot)
    {
        if (slot > NewestSlot)
            return SlotHistoryCheck.Future;
        if (slot < OldestSlot)
            return SlotHistoryCheck.TooOld;

        var bitIndex = slot % MaximumEntries;
        var block = _blocks[(int)(bitIndex / BitsPerBlock)];
        var mask = 1UL << (int)(bitIndex % BitsPerBlock);
        return (block & mask) is not 0 ? SlotHistoryCheck.Found : SlotHistoryCheck.NotFound;
    }
}
