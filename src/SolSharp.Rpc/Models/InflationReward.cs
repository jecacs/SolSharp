using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>A staking reward paid to an address for an epoch, as returned by <c>getInflationReward</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getinflationreward">getInflationReward</seealso>
public sealed record InflationReward
{
    /// <summary>The epoch the reward was paid for.</summary>
    [JsonPropertyName("epoch")]
    public ulong Epoch { get; init; }

    /// <summary>The slot at which the reward was credited.</summary>
    [JsonPropertyName("effectiveSlot")]
    public ulong EffectiveSlot { get; init; }

    /// <summary>The reward amount, in lamports.</summary>
    [JsonPropertyName("amount")]
    public ulong Amount { get; init; }

    /// <summary>The account balance, in lamports, after the reward was applied.</summary>
    [JsonPropertyName("postBalance")]
    public ulong PostBalance { get; init; }

    /// <summary>The vote account commission applied to this reward, when it is a voting reward; otherwise <c>null</c>.</summary>
    [JsonPropertyName("commission")]
    public byte? Commission { get; init; }
}
