using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>A transaction-logs notification payload from <c>logsSubscribe</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/logssubscribe">logsSubscribe</seealso>
public sealed record LogInfo
{
    /// <summary>The transaction signature these logs belong to.</summary>
    [JsonPropertyName("signature")]
    public string Signature { get; init; } = string.Empty;

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The log lines emitted by the transaction.</summary>
    [JsonPropertyName("logs")]
    public IReadOnlyList<string> Logs { get; init; } = [];

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
