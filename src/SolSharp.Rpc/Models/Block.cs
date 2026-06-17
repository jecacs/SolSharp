using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>
/// A confirmed block, as returned by <c>getBlock</c> with transaction details set to signatures: its hashes,
/// slots, time, and the signatures of the transactions it contains.
/// </summary>
public sealed record Block
{
    /// <summary>The block's blockhash (base58).</summary>
    [JsonPropertyName("blockhash")]
    public string Blockhash { get; init; } = string.Empty;

    /// <summary>The blockhash of this block's parent (base58).</summary>
    [JsonPropertyName("previousBlockhash")]
    public string PreviousBlockhash { get; init; } = string.Empty;

    /// <summary>The slot of this block's parent.</summary>
    [JsonPropertyName("parentSlot")]
    public ulong ParentSlot { get; init; }

    /// <summary>The block's height, if the node reported it.</summary>
    [JsonPropertyName("blockHeight")]
    public ulong? BlockHeight { get; init; }

    /// <summary>The block's production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    public long? BlockTime { get; init; }

    /// <summary>The signatures of the transactions in the block, in order.</summary>
    [JsonPropertyName("signatures")]
    public IReadOnlyList<string>? Signatures { get; init; }
}
