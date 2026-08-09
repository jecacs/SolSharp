using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Streaming;

/// <summary>A slot-change notification payload from <c>slotSubscribe</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/websocket/slotsubscribe">slotSubscribe</seealso>
public sealed record SlotInfo
{
    /// <summary>The parent slot.</summary>
    [JsonPropertyName("parent")]
    [JsonRequired]
    public ulong Parent { get; init; }

    /// <summary>The current root slot.</summary>
    [JsonPropertyName("root")]
    [JsonRequired]
    public ulong Root { get; init; }

    /// <summary>The newly set slot.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }
}
