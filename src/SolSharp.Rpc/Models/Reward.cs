using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A reward or debit recorded in transaction or block metadata.</summary>
public sealed record Reward
{
    /// <summary>The rewarded account.</summary>
    [JsonPropertyName("pubkey")]
    public PublicKey PublicKey { get; init; }

    /// <summary>The signed balance change in lamports; negative values are debits.</summary>
    [JsonPropertyName("lamports")]
    public long Lamports { get; init; }

    /// <summary>The account balance after applying <see cref="Lamports"/>.</summary>
    [JsonPropertyName("postBalance")]
    public ulong PostBalance { get; init; }

    /// <summary>The reward type reported by the node, or <c>null</c>.</summary>
    [JsonPropertyName("rewardType")]
    public string? RewardType { get; init; }

    /// <summary>The legacy percentage commission for voting or staking rewards, or <c>null</c>.</summary>
    [JsonPropertyName("commission")]
    public byte? Commission { get; init; }

    /// <summary>The commission in basis points, when reported by nodes supporting SIMD-0291.</summary>
    [JsonPropertyName("commissionBps")]
    public ushort? CommissionBps { get; init; }
}
