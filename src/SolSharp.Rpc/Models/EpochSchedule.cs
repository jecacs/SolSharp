using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's epoch schedule, as returned by <c>getEpochSchedule</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getepochschedule">getEpochSchedule</seealso>
public sealed record EpochSchedule
{
    /// <summary>The maximum number of slots in each epoch.</summary>
    [JsonPropertyName("slotsPerEpoch")]
    [JsonRequired]
    public ulong SlotsPerEpoch { get; init; }

    /// <summary>The number of slots before the start of an epoch at which its leader schedule is computed.</summary>
    [JsonPropertyName("leaderScheduleSlotOffset")]
    [JsonRequired]
    public ulong LeaderScheduleSlotOffset { get; init; }

    /// <summary>Whether epochs start short and grow (the warmup period).</summary>
    [JsonPropertyName("warmup")]
    [JsonRequired]
    public bool Warmup { get; init; }

    /// <summary>The first epoch of normal length (after warmup).</summary>
    [JsonPropertyName("firstNormalEpoch")]
    [JsonRequired]
    public ulong FirstNormalEpoch { get; init; }

    /// <summary>The slot at which <see cref="FirstNormalEpoch"/> begins.</summary>
    [JsonPropertyName("firstNormalSlot")]
    [JsonRequired]
    public ulong FirstNormalSlot { get; init; }
}
