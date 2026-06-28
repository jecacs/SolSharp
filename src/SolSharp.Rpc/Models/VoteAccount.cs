using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>The cluster's vote accounts, split into currently-voting and delinquent, as returned by <c>getVoteAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getvoteaccounts">getVoteAccounts</seealso>
public sealed record VoteAccounts
{
    /// <summary>Vote accounts that have voted recently enough to be considered active.</summary>
    [JsonPropertyName("current")]
    public IReadOnlyList<VoteAccount> Current { get; init; } = [];

    /// <summary>Vote accounts that have not voted recently enough (delinquent).</summary>
    [JsonPropertyName("delinquent")]
    public IReadOnlyList<VoteAccount> Delinquent { get; init; } = [];
}

/// <summary>A validator's vote account, as returned within <c>getVoteAccounts</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getvoteaccounts">getVoteAccounts</seealso>
public sealed record VoteAccount
{
    /// <summary>The vote account address.</summary>
    [JsonPropertyName("votePubkey")]
    public PublicKey VotePubkey { get; init; }

    /// <summary>The validator identity that votes through this account.</summary>
    [JsonPropertyName("nodePubkey")]
    public PublicKey NodePubkey { get; init; }

    /// <summary>The stake, in lamports, delegated to this vote account and active this epoch.</summary>
    [JsonPropertyName("activatedStake")]
    public ulong ActivatedStake { get; init; }

    /// <summary>Whether the vote account is staked for the current epoch.</summary>
    [JsonPropertyName("epochVoteAccount")]
    public bool EpochVoteAccount { get; init; }

    /// <summary>The percentage (0-100) of rewards owed to the validator.</summary>
    [JsonPropertyName("commission")]
    public byte Commission { get; init; }

    /// <summary>The most recent slot this account voted on.</summary>
    [JsonPropertyName("lastVote")]
    public ulong LastVote { get; init; }

    /// <summary>The current root slot for this vote account.</summary>
    [JsonPropertyName("rootSlot")]
    public ulong RootSlot { get; init; }

    /// <summary>Recent earned credits per epoch, each entry being <c>[epoch, credits, previousCredits]</c>.</summary>
    [JsonPropertyName("epochCredits")]
    public IReadOnlyList<IReadOnlyList<long>> EpochCredits { get; init; } = [];
}
