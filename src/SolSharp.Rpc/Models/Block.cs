using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>
/// A confirmed block, as returned by <c>getBlock</c> with transaction details set to signatures: its hashes,
/// slots, time, and the signatures of the transactions it contains.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblock">getBlock</seealso>
public sealed record Block
{
    private string? _blockhash;
    private string? _previousBlockhash;
    private IReadOnlyList<string>? _signatures;

    /// <summary>The block's blockhash (base58).</summary>
    [JsonPropertyName("blockhash")]
    [JsonRequired]
    public string Blockhash
    {
        get => _blockhash ?? throw new InvalidOperationException("The blockhash has not been initialized.");
        init => _blockhash = value ?? throw new JsonException("A block must carry its blockhash.");
    }

    /// <summary>The blockhash of this block's parent (base58).</summary>
    [JsonPropertyName("previousBlockhash")]
    [JsonRequired]
    public string PreviousBlockhash
    {
        get => _previousBlockhash ?? throw new InvalidOperationException("The previous blockhash has not been initialized.");
        init => _previousBlockhash = value ?? throw new JsonException("A block must carry its previous blockhash.");
    }

    /// <summary>The slot of this block's parent.</summary>
    [JsonPropertyName("parentSlot")]
    [JsonRequired]
    public ulong ParentSlot { get; init; }

    /// <summary>The block's height, if the node reported it.</summary>
    [JsonPropertyName("blockHeight")]
    [JsonRequired]
    public ulong? BlockHeight { get; init; }

    /// <summary>The block's production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    [JsonRequired]
    public long? BlockTime { get; init; }

    /// <summary>The number of partitions used for epoch rewards in this block, when applicable.</summary>
    [JsonPropertyName("numRewardPartitions")]
    public ulong? NumRewardPartitions { get; init; }

    /// <summary>The signatures of the transactions in the block, in order.</summary>
    [JsonPropertyName("signatures")]
    [JsonRequired]
    public IReadOnlyList<string> Signatures
    {
        get => _signatures ?? throw new InvalidOperationException("The block signatures have not been initialized.");
        init
        {
            if (value is null || value.Any(static signature => signature is null))
                throw new JsonException("A signatures-only block must carry only non-null signatures.");

            _signatures = value;
        }
    }
}
