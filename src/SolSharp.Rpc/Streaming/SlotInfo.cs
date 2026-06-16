using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>A slot-change notification payload from <c>slotSubscribe</c>.</summary>
public sealed record SlotInfo
{
    /// <summary>The parent slot.</summary>
    [JsonPropertyName("parent")]
    public ulong Parent { get; init; }

    /// <summary>The current root slot.</summary>
    [JsonPropertyName("root")]
    public ulong Root { get; init; }

    /// <summary>The newly set slot.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }
}
