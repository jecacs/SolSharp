using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A new-vote notification payload from <c>voteSubscribe</c> - a vote observed in gossip before it
/// lands in a block.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/votesubscribe">voteSubscribe</seealso>
public sealed record VoteNotification
{
    /// <summary>The identity of the voting validator.</summary>
    [JsonPropertyName("votePubkey")]
    public PublicKey VotePubkey { get; init; }

    /// <summary>The slots the vote covers.</summary>
    [JsonPropertyName("slots")]
    public IReadOnlyList<ulong> Slots { get; init; } = [];

    /// <summary>The hash the vote is for (base58).</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;

    /// <summary>The vote's Unix timestamp in seconds, when the validator attached one.</summary>
    [JsonPropertyName("timestamp")]
    public long? Timestamp { get; init; }

    /// <summary>The signature of the transaction carrying the vote (base58).</summary>
    [JsonPropertyName("signature")]
    public string Signature { get; init; } = string.Empty;
}
