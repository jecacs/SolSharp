using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>A confirmed transaction as returned by <c>getTransaction</c>: where it landed and how it executed.</summary>
public sealed record TransactionResponse
{
    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The estimated production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    public long? BlockTime { get; init; }

    /// <summary>Execution metadata: fee, balances, logs, and any error.</summary>
    [JsonPropertyName("meta")]
    public TransactionMeta? Meta { get; init; }
}

/// <summary>The execution metadata attached to a confirmed transaction.</summary>
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

    /// <summary>The log lines the transaction emitted, if the node returned them.</summary>
    [JsonPropertyName("logMessages")]
    public IReadOnlyList<string>? LogMessages { get; init; }

    /// <summary>The compute units the transaction consumed, if the node reported it.</summary>
    [JsonPropertyName("computeUnitsConsumed")]
    public ulong? ComputeUnitsConsumed { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
