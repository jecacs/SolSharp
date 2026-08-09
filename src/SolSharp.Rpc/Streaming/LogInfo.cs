using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>A transaction-logs notification payload from <c>logsSubscribe</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/logssubscribe">logsSubscribe</seealso>
public sealed record LogInfo
{
    private string? _signature;
    private IReadOnlyList<string>? _logs;

    /// <summary>The transaction signature these logs belong to.</summary>
    [JsonPropertyName("signature")]
    [JsonRequired]
    public string Signature
    {
        get => _signature ?? throw new InvalidOperationException("The log signature has not been initialized.");
        init => _signature = value ?? throw new JsonException("A logs notification must carry a signature.");
    }

    /// <summary>The transaction error, or <c>null</c> if it succeeded.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>The log lines emitted by the transaction.</summary>
    [JsonPropertyName("logs")]
    [JsonRequired]
    public IReadOnlyList<string> Logs
    {
        get => _logs ?? throw new InvalidOperationException("The log lines have not been initialized.");
        init
        {
            if (value is null || value.Any(static entry => entry is null))
                throw new JsonException("A logs notification must carry only non-null log strings.");

            _logs = value;
        }
    }

    /// <summary>True when the transaction failed (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
