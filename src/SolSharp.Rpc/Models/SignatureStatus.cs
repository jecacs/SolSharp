using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The processing status of a transaction signature, as returned by <c>getSignatureStatuses</c>.</summary>
public sealed record SignatureStatus
{
    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The number of blocks since confirmation, or <c>null</c> once the transaction is finalized (rooted).</summary>
    [JsonPropertyName("confirmations")]
    public ulong? Confirmations { get; init; }

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The cluster confirmation level reached: <c>processed</c>, <c>confirmed</c>, or <c>finalized</c>.</summary>
    [JsonPropertyName("confirmationStatus")]
    public string? ConfirmationStatus { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
