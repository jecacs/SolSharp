using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Rpc.Models.Parsed;

namespace SolSharp.Rpc.Models;

/// <summary>The result of simulating a transaction.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/simulatetransaction">simulateTransaction</seealso>
public sealed record SimulateTransactionResult
{
    /// <summary>The transaction error, or <c>null</c> if the simulation succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The log lines the transaction emitted, or <c>null</c> if the node returned none.</summary>
    [JsonPropertyName("logs")]
    public IReadOnlyList<string>? Logs { get; init; }

    /// <summary>The number of compute units the transaction consumed, if the node reported it.</summary>
    [JsonPropertyName("unitsConsumed")]
    public ulong? UnitsConsumed { get; init; }

    /// <summary>The total loaded-account data size in bytes, when reported.</summary>
    [JsonPropertyName("loadedAccountsDataSize")]
    public uint? LoadedAccountsDataSize { get; init; }

    /// <summary>The requested post-simulation account states, or <c>null</c> when none were requested.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<AccountInfo?>? Accounts { get; init; }

    /// <summary>Data returned by a program, or <c>null</c> when no program set return data.</summary>
    [JsonPropertyName("returnData")]
    public TransactionReturnData? ReturnData { get; init; }

    /// <summary>Parsed inner instructions, when requested.</summary>
    [JsonPropertyName("innerInstructions")]
    public IReadOnlyList<ParsedInnerInstructions>? InnerInstructions { get; init; }

    /// <summary>The replacement blockhash used when recent-blockhash replacement was requested.</summary>
    [JsonPropertyName("replacementBlockhash")]
    public LatestBlockhash? ReplacementBlockhash { get; init; }

    /// <summary>The fee the simulated transaction would pay, when reported.</summary>
    [JsonPropertyName("fee")]
    public ulong? Fee { get; init; }

    /// <summary>Account lamport balances before simulation, when reported.</summary>
    [JsonPropertyName("preBalances")]
    public IReadOnlyList<ulong>? PreBalances { get; init; }

    /// <summary>Account lamport balances after simulation, when reported.</summary>
    [JsonPropertyName("postBalances")]
    public IReadOnlyList<ulong>? PostBalances { get; init; }

    /// <summary>SPL token balances before simulation, when reported.</summary>
    [JsonPropertyName("preTokenBalances")]
    public IReadOnlyList<TokenBalance>? PreTokenBalances { get; init; }

    /// <summary>SPL token balances after simulation, when reported.</summary>
    [JsonPropertyName("postTokenBalances")]
    public IReadOnlyList<TokenBalance>? PostTokenBalances { get; init; }

    /// <summary>Addresses loaded from lookup tables by a versioned transaction.</summary>
    [JsonPropertyName("loadedAddresses")]
    public LoadedAddresses? LoadedAddresses { get; init; }

    /// <summary>True when the simulation reported an error (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if the simulation succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);
}
