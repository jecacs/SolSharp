using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>Cluster epoch and slot information, as returned by <c>getEpochInfo</c>.</summary>
public sealed record EpochInfo
{
    /// <summary>The current slot.</summary>
    [JsonPropertyName("absoluteSlot")]
    public ulong AbsoluteSlot { get; init; }

    /// <summary>The current block height.</summary>
    [JsonPropertyName("blockHeight")]
    public ulong BlockHeight { get; init; }

    /// <summary>The current epoch.</summary>
    [JsonPropertyName("epoch")]
    public ulong Epoch { get; init; }

    /// <summary>The current slot's index relative to the start of the epoch.</summary>
    [JsonPropertyName("slotIndex")]
    public ulong SlotIndex { get; init; }

    /// <summary>The number of slots in the current epoch.</summary>
    [JsonPropertyName("slotsInEpoch")]
    public ulong SlotsInEpoch { get; init; }

    /// <summary>The total number of transactions processed without error since genesis, if the node reports it.</summary>
    [JsonPropertyName("transactionCount")]
    public ulong? TransactionCount { get; init; }
}
