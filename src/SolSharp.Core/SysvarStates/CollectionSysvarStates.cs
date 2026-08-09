using SolSharp.Core.Primitives;

namespace SolSharp.Core.SysvarStates;

/// <summary>A slot and block hash from the slot-hashes sysvar.</summary>
/// <param name="Slot">The slot.</param>
/// <param name="Hash">The slot's block hash.</param>
public readonly record struct SlotHashEntry(ulong Slot, Hash Hash);

/// <summary>The bounded bincode state of the slot-hashes sysvar.</summary>
public sealed record SlotHashesSysvarState
{
    /// <summary>The maximum number of entries retained by the runtime.</summary>
    public const int MaximumEntries = 512;

    /// <summary>The maximum serialized account-data length.</summary>
    public const int MaximumDataLength = 20_488;

    private SlotHashesSysvarState(IReadOnlyList<SlotHashEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>The slot hashes in their serialized order.</summary>
    public IReadOnlyList<SlotHashEntry> Entries { get; }

    /// <summary>Decodes a bounded bincode slot-hashes account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded slot hashes.</returns>
    /// <exception cref="ArgumentException">The data is malformed, truncated, over the runtime limit, or has trailing bytes.</exception>
    public static SlotHashesSysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var count = reader.ReadBoundedCount(MaximumEntries, "Slot hashes");
        var entries = new SlotHashEntry[count];
        for (var i = 0; i < entries.Length; i++)
            entries[i] = new SlotHashEntry(reader.ReadUInt64(), reader.ReadHash());
        reader.EnsureEnd();
        return new SlotHashesSysvarState(entries);
    }
}

/// <summary>Stake activation totals recorded for one epoch.</summary>
/// <param name="Effective">The effective stake.</param>
/// <param name="Activating">The stake still activating.</param>
/// <param name="Deactivating">The stake still deactivating.</param>
public readonly record struct StakeHistoryEntry(ulong Effective, ulong Activating, ulong Deactivating);

/// <summary>An epoch and its stake activation totals.</summary>
/// <param name="Epoch">The epoch.</param>
/// <param name="Entry">The stake totals.</param>
public readonly record struct StakeHistoryEpoch(ulong Epoch, StakeHistoryEntry Entry);

/// <summary>The bounded bincode state of the stake-history sysvar.</summary>
public sealed record StakeHistorySysvarState
{
    /// <summary>The maximum number of entries retained by the runtime.</summary>
    public const int MaximumEntries = 512;

    /// <summary>The maximum serialized account-data length.</summary>
    public const int MaximumDataLength = 16_392;

    private StakeHistorySysvarState(IReadOnlyList<StakeHistoryEpoch> entries)
    {
        Entries = entries;
    }

    /// <summary>The epochs and stake totals in their serialized order.</summary>
    public IReadOnlyList<StakeHistoryEpoch> Entries { get; }

    /// <summary>Decodes a bounded bincode stake-history account.</summary>
    /// <param name="data">The complete account data.</param>
    /// <returns>The decoded stake history.</returns>
    /// <exception cref="ArgumentException">The data is malformed, truncated, over the runtime limit, or has trailing bytes.</exception>
    public static StakeHistorySysvarState Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SysvarBincodeReader(data);
        var count = reader.ReadBoundedCount(MaximumEntries, "Stake history");
        var entries = new StakeHistoryEpoch[count];
        for (var i = 0; i < entries.Length; i++)
        {
            var epoch = reader.ReadUInt64();
            var entry = new StakeHistoryEntry(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
            entries[i] = new StakeHistoryEpoch(epoch, entry);
        }

        reader.EnsureEnd();
        return new StakeHistorySysvarState(entries);
    }
}
