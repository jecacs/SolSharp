using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Rpc.Models.Parsed;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A <c>blockSubscribe</c> notification carrying a full <c>jsonParsed</c> block: the block produced at
/// <see cref="Slot"/>, or just the slot and an error when the block could not be produced.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</seealso>
public sealed record ParsedBlockNotification : IJsonOnDeserialized
{
    /// <summary>The slot this notification is for.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The error that prevented the block from being produced, or <c>null</c> on success.</summary>
    [JsonPropertyName("err")]
    [JsonRequired]
    public JsonElement? Err { get; init; }

    /// <summary>The produced block with parsed transactions, or <c>null</c> when <see cref="Err"/> is set.</summary>
    [JsonPropertyName("block")]
    [JsonRequired]
    public ParsedBlock? Block { get; init; }

    /// <summary>True when the block could not be produced (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };

    /// <inheritdoc/>
    public void OnDeserialized()
    {
        var hasError = Err is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };
        if ((Block is not null) == hasError)
            throw new JsonException("A parsed-block notification must carry exactly one of block or error.");
    }
}
