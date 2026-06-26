using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>One entry from <c>getSignaturesForAddress</c>: a confirmed transaction that touched the queried address.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getsignaturesforaddress">getSignaturesForAddress</seealso>
public sealed record SignatureInfo
{
    /// <summary>The transaction signature, base58.</summary>
    [JsonPropertyName("signature")]
    public string Signature { get; init; } = string.Empty;

    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The memo attached to the transaction, or <c>null</c> if there was none.</summary>
    [JsonPropertyName("memo")]
    public string? Memo { get; init; }

    /// <summary>The estimated production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    public long? BlockTime { get; init; }

    /// <summary>The cluster confirmation status (<c>processed</c>, <c>confirmed</c>, or <c>finalized</c>), if present.</summary>
    [JsonPropertyName("confirmationStatus")]
    public string? ConfirmationStatus { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
