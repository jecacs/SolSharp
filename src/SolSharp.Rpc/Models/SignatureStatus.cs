using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The processing status of a transaction signature, as returned by <c>getSignatureStatuses</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getsignaturestatuses">getSignatureStatuses</seealso>
public sealed record SignatureStatus : IJsonOnDeserialized
{
    private string? _confirmationStatus;
    private JsonElement? _status;
    private bool _statusIndicatesError;

    /// <summary>The slot the transaction was processed in.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The number of blocks since confirmation, or <c>null</c> once the transaction is finalized (rooted).</summary>
    [JsonPropertyName("confirmations")]
    [JsonRequired]
    public ulong? Confirmations { get; init; }

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
    public JsonElement? Status
    {
        get => _status;
        init
        {
            if (value is not { ValueKind: JsonValueKind.Object } status)
            {
                throw new JsonException("A signature status must carry exactly one Result branch, Ok or Err.");
            }

            var hasOk = status.TryGetProperty("Ok", out var ok);
            var hasError = status.TryGetProperty(nameof(Err), out var error);
            if (status.EnumerateObject().Count() != 1 ||
                hasOk == hasError ||
                (hasOk && ok.ValueKind is not JsonValueKind.Null) ||
                (hasError && error.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined))
            {
                throw new JsonException("A signature status must carry exactly one canonical Result branch, Ok or Err.");
            }

            _status = value;
            _statusIndicatesError = hasError;
        }
    }

    /// <summary>The cluster confirmation level reached: <c>processed</c>, <c>confirmed</c>, or <c>finalized</c>.</summary>
    [JsonPropertyName("confirmationStatus")]
    public string? ConfirmationStatus
    {
        get => _confirmationStatus;
        init
        {
            if (value is not null and not ("processed" or "confirmed" or "finalized"))
                throw new JsonException($"Unknown transaction confirmation status '{value}'.");

            _confirmationStatus = value;
        }
    }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <summary>The decoded transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonIgnore]
    public TransactionError? Error => TransactionError.Parse(Err);

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        if (_statusIndicatesError != IsError)
            throw new JsonException("A signature status carried inconsistent status and err fields.");
    }
}
