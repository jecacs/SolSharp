using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's vote accounts, split into currently-voting and delinquent, as returned by <c>getVoteAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getvoteaccounts">getVoteAccounts</seealso>
public sealed record VoteAccounts
{
    private IReadOnlyList<VoteAccount>? _current;
    private IReadOnlyList<VoteAccount>? _delinquent;

    /// <summary>Vote accounts that have voted recently enough to be considered active.</summary>
    [JsonPropertyName("current")]
    [JsonRequired]
    public IReadOnlyList<VoteAccount> Current
    {
        get => _current!;
        init => _current = RequireVoteAccounts(value, "current");
    }

    /// <summary>Vote accounts that have not voted recently enough (delinquent).</summary>
    [JsonPropertyName("delinquent")]
    [JsonRequired]
    public IReadOnlyList<VoteAccount> Delinquent
    {
        get => _delinquent!;
        init => _delinquent = RequireVoteAccounts(value, "delinquent");
    }

    private static IReadOnlyList<VoteAccount> RequireVoteAccounts(
        IReadOnlyList<VoteAccount>? accounts,
        string name)
    {
        if (accounts is null || accounts.Any(static account => account is null))
            throw new JsonException($"A vote-account response must carry only non-null {name} entries.");

        return accounts;
    }
}

/// <summary>A validator's vote account, as returned within <c>getVoteAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getvoteaccounts">getVoteAccounts</seealso>
public sealed record VoteAccount
{
    private IReadOnlyList<VoteEpochCredit>? _epochCredits;

    /// <summary>The vote account address.</summary>
    [JsonPropertyName("votePubkey")]
    [JsonRequired]
    public PublicKey VotePubkey { get; init; }

    /// <summary>The validator identity that votes through this account.</summary>
    [JsonPropertyName("nodePubkey")]
    [JsonRequired]
    public PublicKey NodePubkey { get; init; }

    /// <summary>The stake, in lamports, delegated to this vote account and active this epoch.</summary>
    [JsonPropertyName("activatedStake")]
    [JsonRequired]
    public ulong ActivatedStake { get; init; }

    /// <summary>Whether the vote account is staked for the current epoch.</summary>
    [JsonPropertyName("epochVoteAccount")]
    [JsonRequired]
    public bool EpochVoteAccount { get; init; }

    /// <summary>The percentage (0-100) of rewards owed to the validator.</summary>
    [JsonPropertyName("commission")]
    [JsonRequired]
    public byte Commission { get; init; }

    /// <summary>
    /// The raw commission in basis points, when reported by nodes supporting SIMD-0291; otherwise <c>null</c>.
    /// </summary>
    [JsonPropertyName("inflationRewardsCommissionBps")]
    public ushort? InflationRewardsCommissionBps { get; init; }

    /// <summary>The most recent slot this account voted on.</summary>
    [JsonPropertyName("lastVote")]
    [JsonRequired]
    public ulong LastVote { get; init; }

    /// <summary>The current root slot for this vote account.</summary>
    [JsonPropertyName("rootSlot")]
    [JsonRequired]
    public ulong RootSlot { get; init; }

    /// <summary>Recent earned credits per epoch, each entry being <c>[epoch, credits, previousCredits]</c>.</summary>
    [JsonPropertyName("epochCredits")]
    [JsonRequired]
    public IReadOnlyList<VoteEpochCredit> EpochCredits
    {
        get => _epochCredits!;
        init => _epochCredits = value ?? throw new JsonException("A vote account must carry epoch credits.");
    }
}
