using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The result of simulating a transaction.</summary>
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

    /// <summary>True when the simulation reported an error (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if the simulation succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);
}
