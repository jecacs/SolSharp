using System.Text.Json;
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
    private IReadOnlyList<ulong>? _slots;
    private string? _hash;
    private string? _signature;

    /// <summary>The vote-account address that produced the vote.</summary>
    [JsonPropertyName("votePubkey")]
    [JsonRequired]
    public PublicKey VotePubkey { get; init; }

    /// <summary>The slots the vote covers.</summary>
    [JsonPropertyName("slots")]
    [JsonRequired]
    public IReadOnlyList<ulong> Slots
    {
        get => _slots ?? throw new InvalidOperationException("The voted slots have not been initialized.");
        init => _slots = value ?? throw new JsonException("A vote notification must carry its voted slots.");
    }

    /// <summary>The hash the vote is for (base58).</summary>
    [JsonPropertyName("hash")]
    [JsonRequired]
    public string Hash
    {
        get => _hash ?? throw new InvalidOperationException("The vote hash has not been initialized.");
        init => _hash = value ?? throw new JsonException("A vote notification must carry a vote hash.");
    }

    /// <summary>The vote's Unix timestamp in seconds, when the validator attached one.</summary>
    [JsonPropertyName("timestamp")]
    [JsonRequired]
    public long? Timestamp { get; init; }

    /// <summary>The signature of the transaction carrying the vote (base58).</summary>
    [JsonPropertyName("signature")]
    [JsonRequired]
    public string Signature
    {
        get => _signature ?? throw new InvalidOperationException("The vote signature has not been initialized.");
        init => _signature = value ?? throw new JsonException("A vote notification must carry a signature.");
    }
}
