using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// The execution metadata of a <c>jsonParsed</c> transaction: fee, balances, token balances, logs, inner
/// instructions, and any error. Collections are nullable because the node omits or nulls some of them.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record ParsedTransactionMeta
{
    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The fee charged, in lamports.</summary>
    [JsonPropertyName("fee")]
    public ulong Fee { get; init; }

    /// <summary>Account lamport balances before the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("preBalances")]
    public IReadOnlyList<ulong>? PreBalances { get; init; }

    /// <summary>Account lamport balances after the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("postBalances")]
    public IReadOnlyList<ulong>? PostBalances { get; init; }

    /// <summary>The inner (CPI) instructions invoked, grouped by their top-level instruction; <c>null</c> if the node omitted them.</summary>
    [JsonPropertyName("innerInstructions")]
    public IReadOnlyList<ParsedInnerInstructions>? InnerInstructions { get; init; }

    /// <summary>The log lines the transaction emitted, or <c>null</c> if the node did not return them.</summary>
    [JsonPropertyName("logMessages")]
    public IReadOnlyList<string>? LogMessages { get; init; }

    /// <summary>SPL token balances before the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("preTokenBalances")]
    public IReadOnlyList<TokenBalance>? PreTokenBalances { get; init; }

    /// <summary>SPL token balances after the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("postTokenBalances")]
    public IReadOnlyList<TokenBalance>? PostTokenBalances { get; init; }

    /// <summary>The accounts a versioned transaction loaded from address lookup tables, or <c>null</c> for a legacy transaction.</summary>
    [JsonPropertyName("loadedAddresses")]
    public LoadedAddresses? LoadedAddresses { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);
}
