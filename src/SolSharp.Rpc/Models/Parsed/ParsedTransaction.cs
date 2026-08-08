using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// A <c>jsonParsed</c> transaction as returned by <c>getTransaction</c> (and within <c>getBlock</c>): its
/// signatures, the node-decoded message, execution metadata, and - for a single fetch - where and when it landed.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
[JsonConverter(typeof(ParsedTransactionJsonConverter))]
public sealed record ParsedTransaction
{
    /// <summary>The transaction's signatures (base58); the first is its id.</summary>
    public IReadOnlyList<string> Signatures { get; init; } = [];

    /// <summary>The node-decoded message: accounts, instructions, and recent blockhash.</summary>
    public ParsedMessage Message { get; init; } = new();

    /// <summary>The execution metadata, or <c>null</c> if the node did not return it.</summary>
    public ParsedTransactionMeta? Meta { get; init; }

    /// <summary>
    /// The slot the transaction landed in. Populated by <c>GetParsedTransactionAsync</c> and
    /// <c>GetParsedBlockAsync</c>; <c>null</c> in streamed block notifications.
    /// </summary>
    public ulong? Slot { get; init; }

    /// <summary>The block production time as Unix seconds, when known; otherwise <c>null</c>.</summary>
    public long? BlockTime { get; init; }

    /// <summary>
    /// The transaction's index within its block. Read from <c>getTransaction</c> when reported and
    /// derived from ledger order by <c>GetParsedBlockAsync</c>; <c>null</c> in streamed block notifications.
    /// </summary>
    public uint? TransactionIndex { get; init; }

    /// <summary>The JSON transaction version: <c>"legacy"</c> or a numeric version such as <c>0</c>.</summary>
    public JsonElement? Version { get; init; }
}
