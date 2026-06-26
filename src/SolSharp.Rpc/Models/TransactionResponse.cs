using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A confirmed transaction as returned by <c>getTransaction</c>: where it landed, its bytes, and how it executed.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record TransactionResponse
{
    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The estimated production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    public long? BlockTime { get; init; }

    /// <summary>
    /// The transaction's wire bytes, decoded from the node's base64 form; pass to
    /// <c>Transaction.Deserialize</c> (in SolSharp.Programs) to read its message, accounts, and instructions.
    /// </summary>
    [JsonPropertyName("transaction")]
    [JsonConverter(typeof(Base64TupleJsonConverter))]
    public byte[]? Transaction { get; init; }

    /// <summary>Execution metadata: fee, balances, token balances, logs, inner instructions, and any error.</summary>
    [JsonPropertyName("meta")]
    public TransactionMeta? Meta { get; init; }
}

/// <summary>The execution metadata attached to a confirmed transaction.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gettransaction">getTransaction</seealso>
public sealed record TransactionMeta
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

    /// <summary>SPL token balances before the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("preTokenBalances")]
    public IReadOnlyList<TokenBalance>? PreTokenBalances { get; init; }

    /// <summary>SPL token balances after the transaction, for the accounts that hold tokens.</summary>
    [JsonPropertyName("postTokenBalances")]
    public IReadOnlyList<TokenBalance>? PostTokenBalances { get; init; }

    /// <summary>The inner (CPI) instructions invoked, grouped by their top-level instruction; <c>null</c> if the node omitted them.</summary>
    [JsonPropertyName("innerInstructions")]
    public IReadOnlyList<InnerInstructionGroup>? InnerInstructions { get; init; }

    /// <summary>The accounts a versioned transaction loaded from address lookup tables, or <c>null</c> for a legacy transaction.</summary>
    [JsonPropertyName("loadedAddresses")]
    public LoadedAddresses? LoadedAddresses { get; init; }

    /// <summary>The log lines the transaction emitted, if the node returned them.</summary>
    [JsonPropertyName("logMessages")]
    public IReadOnlyList<string>? LogMessages { get; init; }

    /// <summary>The compute units the transaction consumed, if the node reported it.</summary>
    [JsonPropertyName("computeUnitsConsumed")]
    public ulong? ComputeUnitsConsumed { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);
}

/// <summary>A pre- or post-execution SPL token balance snapshot from a transaction's metadata.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record TokenBalance
{
    /// <summary>The index, into the transaction's account list, of the token account this balance is for.</summary>
    [JsonPropertyName("accountIndex")]
    public int AccountIndex { get; init; }

    /// <summary>The token mint.</summary>
    [JsonPropertyName("mint")]
    public PublicKey Mint { get; init; }

    /// <summary>The token account's owner, if the node reported it.</summary>
    [JsonPropertyName("owner")]
    public PublicKey? Owner { get; init; }

    /// <summary>The token program that owns the account (SPL Token or Token-2022), if the node reported it.</summary>
    [JsonPropertyName("programId")]
    public PublicKey? ProgramId { get; init; }

    /// <summary>The balance, in base units and as a UI amount.</summary>
    [JsonPropertyName("uiTokenAmount")]
    public TokenAmount UiTokenAmount { get; init; } = new();
}

/// <summary>The inner (CPI) instructions invoked under one top-level instruction.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record InnerInstructionGroup
{
    /// <summary>The index of the top-level instruction these inner instructions were invoked from.</summary>
    [JsonPropertyName("index")]
    public int Index { get; init; }

    /// <summary>The inner instructions, in invocation order.</summary>
    [JsonPropertyName("instructions")]
    public IReadOnlyList<InnerInstruction> Instructions { get; init; } = [];
}

/// <summary>One compiled inner instruction, as returned with base64 transaction encoding.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record InnerInstruction
{
    /// <summary>The index, into the transaction's account list, of the invoked program.</summary>
    [JsonPropertyName("programIdIndex")]
    public int ProgramIdIndex { get; init; }

    /// <summary>The indices, into the transaction's account list, of the accounts passed to the instruction.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<int> Accounts { get; init; } = [];

    /// <summary>The instruction data, base58-encoded.</summary>
    [JsonPropertyName("data")]
    public string Data { get; init; } = string.Empty;

    /// <summary>The CPI stack height at which the instruction ran, if the node reported it.</summary>
    [JsonPropertyName("stackHeight")]
    public int? StackHeight { get; init; }
}

/// <summary>The accounts a versioned transaction loaded from address lookup tables.</summary>
/// <seealso href="https://solana.com/docs/rpc/json-structures">Solana RPC JSON structures</seealso>
public sealed record LoadedAddresses
{
    /// <summary>The writable accounts loaded from lookup tables.</summary>
    [JsonPropertyName("writable")]
    public IReadOnlyList<PublicKey> Writable { get; init; } = [];

    /// <summary>The read-only accounts loaded from lookup tables.</summary>
    [JsonPropertyName("readonly")]
    public IReadOnlyList<PublicKey> Readonly { get; init; } = [];
}
