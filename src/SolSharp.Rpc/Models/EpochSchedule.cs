using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's epoch schedule, as returned by <c>getEpochSchedule</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getepochschedule">getEpochSchedule</seealso>
public sealed record EpochSchedule
{
    /// <summary>The maximum number of slots in each epoch.</summary>
    [JsonPropertyName("slotsPerEpoch")]
    public ulong SlotsPerEpoch { get; init; }

    /// <summary>The number of slots before the start of an epoch at which its leader schedule is computed.</summary>
    [JsonPropertyName("leaderScheduleSlotOffset")]
    public ulong LeaderScheduleSlotOffset { get; init; }

    /// <summary>Whether epochs start short and grow (the warmup period).</summary>
    [JsonPropertyName("warmup")]
    public bool Warmup { get; init; }

    /// <summary>The first epoch of normal length (after warmup).</summary>
    [JsonPropertyName("firstNormalEpoch")]
    public ulong FirstNormalEpoch { get; init; }

    /// <summary>The slot at which <see cref="FirstNormalEpoch"/> begins.</summary>
    [JsonPropertyName("firstNormalSlot")]
    public ulong FirstNormalSlot { get; init; }
}
