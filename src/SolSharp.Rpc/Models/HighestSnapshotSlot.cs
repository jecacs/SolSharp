using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The node's highest snapshot slots, as returned by <c>getHighestSnapshotSlot</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/gethighestsnapshotslot">getHighestSnapshotSlot</seealso>
public sealed record HighestSnapshotSlot
{
    /// <summary>The highest slot the node has a full snapshot for.</summary>
    [JsonPropertyName("full")]
    [JsonRequired]
    public ulong Full { get; init; }

    /// <summary>The highest slot with an incremental snapshot based on <see cref="Full"/>, if any.</summary>
    [JsonPropertyName("incremental")]
    public ulong? Incremental { get; init; }
}
