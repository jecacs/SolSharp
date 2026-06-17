using System.Text.Json;
using System.Text.Json.Serialization;
using SolSharp.Rpc.Models;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A <c>blockSubscribe</c> notification: the block produced at <see cref="Slot"/>, or just the slot and an
/// error when the block could not be produced.
/// </summary>
public sealed record BlockNotification
{
    /// <summary>The slot this notification is for.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }

    /// <summary>The error that prevented the block from being produced, or <c>null</c> on success.</summary>
    [JsonPropertyName("err")]
    public JsonElement? Err { get; init; }

    /// <summary>The produced block, or <c>null</c> when <see cref="Err"/> is set.</summary>
    [JsonPropertyName("block")]
    public Block? Block { get; init; }

    /// <summary>True when the block could not be produced (<see cref="Err"/> is present).</summary>
    [JsonIgnore]
    public bool IsError => Err is { ValueKind: not JsonValueKind.Null };
}
