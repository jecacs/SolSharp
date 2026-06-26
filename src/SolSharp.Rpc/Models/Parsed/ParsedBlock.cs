using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// A confirmed block with full <c>jsonParsed</c> transactions, as returned by <c>getBlock</c> with
/// <c>transactionDetails=full</c> and <c>encoding=jsonParsed</c>.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblock">getBlock</seealso>
public sealed record ParsedBlock
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

    /// <summary>
    /// The block's transactions, decoded. <see cref="ParsedTransaction.Slot"/> and
    /// <see cref="ParsedTransaction.BlockTime"/> are filled in from the block by <c>GetParsedBlockAsync</c>.
    /// </summary>
    [JsonPropertyName("transactions")]
    public IReadOnlyList<ParsedTransaction> Transactions { get; init; } = [];
}
