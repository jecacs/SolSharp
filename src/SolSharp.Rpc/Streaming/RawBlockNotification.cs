using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A configurable <c>blockSubscribe</c> update whose block body is retained as JSON because its schema
/// depends on the requested encoding and transaction detail level.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</seealso>
public sealed record RawBlockNotification : IJsonOnDeserialized
{
    /// <summary>The slot this notification is for.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The error that prevented the block from being produced, or <c>null</c> on success.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>The configured block JSON, or <c>null</c> when <see cref="Err"/> is set.</summary>
    [JsonPropertyName("block")]
    [JsonRequired]
    public JsonElement? Block { get; init; }

    /// <summary>True when the block could not be produced.</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        var hasError = Err is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };
        var hasBlock = Block is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };
        if (hasBlock == hasError)
            throw new JsonException("A raw block notification must carry exactly one of block or error.");
    }
}
