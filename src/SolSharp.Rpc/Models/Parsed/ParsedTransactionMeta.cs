using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models.Parsed;

/// <summary>
/// The execution metadata of a <c>jsonParsed</c> transaction: fee, balances, token balances, logs, inner
/// instructions, and any error. Collections are nullable because the node omits or nulls some of them.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record ParsedTransactionMeta : IJsonOnDeserialized
{
    private JsonElement _status;
    private IReadOnlyList<ulong>? _preBalances;
    private IReadOnlyList<ulong>? _postBalances;

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>
    /// The deprecated result-shaped status field retained by the node for compatibility; prefer
    /// <see cref="Err"/> or <see cref="Error"/> for new code.
    /// </summary>
    [JsonPropertyName("status")]
    [JsonRequired]
    public JsonElement Status
    {
        get => _status;
        init => _status = value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? throw new JsonException("Parsed transaction metadata must carry a non-null status.")
            : value;
    }

    /// <summary>The fee charged, in lamports.</summary>
    [JsonPropertyName("fee")]
    [JsonRequired]
    public ulong Fee { get; init; }

    /// <summary>Account lamport balances before the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("preBalances")]
    [JsonRequired]
    public IReadOnlyList<ulong> PreBalances
    {
        get => _preBalances!;
        init => _preBalances = value ?? throw new JsonException("Parsed transaction metadata must carry pre-balances.");
    }

    /// <summary>Account lamport balances after the transaction, indexed by the message's account list.</summary>
    [JsonPropertyName("postBalances")]
    [JsonRequired]
    public IReadOnlyList<ulong> PostBalances
    {
        get => _postBalances!;
        init => _postBalances = value ?? throw new JsonException("Parsed transaction metadata must carry post-balances.");
    }

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

    /// <summary>The compute units the transaction consumed, when reported.</summary>
    [JsonPropertyName("computeUnitsConsumed")]
    public ulong? ComputeUnitsConsumed { get; init; }

    /// <summary>The transaction cost units, when reported.</summary>
    [JsonPropertyName("costUnits")]
    public ulong? CostUnits { get; init; }

    /// <summary>Data returned by a program, or <c>null</c> when no program set return data.</summary>
    [JsonPropertyName("returnData")]
    public TransactionReturnData? ReturnData { get; init; }

    /// <summary>Rewards and debits recorded while processing the transaction, when reported.</summary>
    [JsonPropertyName("rewards")]
    public IReadOnlyList<Reward>? Rewards { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        TransactionStatusValidator.Validate(Err, Status);
        RpcCollectionValidator.ValidateOptional(InnerInstructions, "parsed inner-instruction groups");
        RpcCollectionValidator.ValidateOptional(LogMessages, "parsed log messages");
        RpcCollectionValidator.ValidateOptional(PreTokenBalances, "parsed pre-token balances");
        RpcCollectionValidator.ValidateOptional(PostTokenBalances, "parsed post-token balances");
        RpcCollectionValidator.ValidateOptional(Rewards, "parsed rewards");
    }
}
