using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A <c>signatureSubscribe</c> notification: delivered once, when the subscribed transaction reaches the
/// requested commitment. The node unsubscribes automatically after sending it.
/// </summary>
public sealed record SignatureNotification
{
    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
