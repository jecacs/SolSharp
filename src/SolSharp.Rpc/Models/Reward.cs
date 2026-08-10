using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A reward or debit recorded in transaction or block metadata.</summary>
public sealed record Reward
{
    /// <summary>The rewarded account.</summary>
    [JsonPropertyName("pubkey")]
    [JsonRequired]
    public PublicKey PublicKey { get; init; }

    /// <summary>The signed balance change in lamports; negative values are debits.</summary>
    [JsonPropertyName("lamports")]
    [JsonRequired]
    public long Lamports { get; init; }

    /// <summary>The account balance after applying <see cref="Lamports"/>.</summary>
    [JsonPropertyName("postBalance")]
    [JsonRequired]
    public ulong PostBalance { get; init; }

    /// <summary>
    /// The pinned reward variant: <c>Fee</c>, <c>Rent</c>, <c>Staking</c>, <c>Voting</c>,
    /// <c>DeactivatedStake</c>, or <c>null</c>.
    /// </summary>
    [JsonPropertyName("rewardType")]
    public string? RewardType
    {
        get;
        init
        {
            if (value is not null and not "Fee" and not "Rent" and not "Staking" and not "Voting" and not "DeactivatedStake")
                throw new JsonException("Unknown reward type.");

            field = value;
        }
    }

    /// <summary>The legacy percentage commission for voting or staking rewards, or <c>null</c>.</summary>
    [JsonPropertyName("commission")]
    public byte? Commission { get; init; }

    /// <summary>The commission in basis points, when reported by nodes supporting SIMD-0291.</summary>
    [JsonPropertyName("commissionBps")]
    public ushort? CommissionBps { get; init; }
}
