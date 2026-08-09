using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// A confirmed block with full <c>jsonParsed</c> transactions, as returned by <c>getBlock</c> with
/// <c>transactionDetails=full</c> and <c>encoding=jsonParsed</c>.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getblock">getBlock</seealso>
public sealed record ParsedBlock
{
    private string? _blockhash;
    private string? _previousBlockhash;
    private IReadOnlyList<ParsedTransaction>? _transactions;

    /// <summary>The block's blockhash (base58).</summary>
    [JsonPropertyName("blockhash")]
    [JsonRequired]
    public string Blockhash
    {
        get => _blockhash!;
        init => _blockhash = value ?? throw new JsonException("A parsed block must carry a blockhash.");
    }

    /// <summary>The blockhash of this block's parent (base58).</summary>
    [JsonPropertyName("previousBlockhash")]
    [JsonRequired]
    public string PreviousBlockhash
    {
        get => _previousBlockhash!;
        init => _previousBlockhash = value ?? throw new JsonException("A parsed block must carry a previous blockhash.");
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

    /// <summary>
    /// The block's transactions, decoded. <see cref="ParsedTransaction.Slot"/> and
    /// <see cref="ParsedTransaction.BlockTime"/> are filled in from the block, and
    /// <see cref="ParsedTransaction.TransactionIndex"/> from its ledger order, by <c>GetParsedBlockAsync</c>.
    /// </summary>
    [JsonPropertyName("transactions")]
    [JsonRequired]
    public IReadOnlyList<ParsedTransaction> Transactions
    {
        get => _transactions!;
        init
        {
            if (value is null || value.Any(static transaction => transaction is null))
                throw new JsonException("A parsed block must carry only non-null transactions.");

            _transactions = value;
        }
    }
}
