using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>One entry from <c>getSignaturesForAddress</c>: a confirmed transaction that touched the queried address.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getsignaturesforaddress">getSignaturesForAddress</seealso>
public sealed record SignatureInfo
{
    /// <summary>The transaction signature, base58.</summary>
    [JsonPropertyName("signature")]
    [JsonRequired]
    public string Signature
    {
        get => field ?? throw new InvalidOperationException("The signature entry has not been initialized.");
        init => field = value ?? throw new JsonException("A signature entry must carry its signature.");
    }

    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>The memo attached to the transaction, or <c>null</c> if there was none.</summary>
    [JsonPropertyName("memo")]
    [JsonRequired]
    public string? Memo { get; init; }

    /// <summary>The estimated production time as Unix seconds, or <c>null</c> if not available.</summary>
    [JsonPropertyName("blockTime")]
    [JsonRequired]
    public long? BlockTime { get; init; }

    /// <summary>The cluster confirmation status (<c>processed</c>, <c>confirmed</c>, or <c>finalized</c>), if present.</summary>
    [JsonPropertyName("confirmationStatus")]
    [JsonRequired]
    public string? ConfirmationStatus
    {
        get;
        init
        {
            if (value is not null and not ("processed" or "confirmed" or "finalized"))
                throw new JsonException($"Unknown transaction confirmation status '{value}'.");

            field = value;
        }
    }

    /// <summary>The transaction's index within its block, when reported.</summary>
    [JsonPropertyName("transactionIndex")]
    public uint? TransactionIndex { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
