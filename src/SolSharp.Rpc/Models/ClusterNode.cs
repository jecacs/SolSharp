using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A node participating in the cluster, as returned by <c>getClusterNodes</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getclusternodes">getClusterNodes</seealso>
public sealed record ClusterNode
{
    /// <summary>The node's identity public key.</summary>
    [JsonPropertyName("pubkey")]
    [JsonRequired]
    public PublicKey Pubkey { get; init; }

    /// <summary>The node's gossip network address (host:port), or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("gossip")]
    public string? Gossip { get; init; }

    /// <summary>The node's TVU (transaction validation unit) address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tvu")]
    public string? Tvu { get; init; }

    /// <summary>The node's TPU (transaction processing unit) address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpu")]
    public string? Tpu { get; init; }

    /// <summary>The node's QUIC TPU address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpuQuic")]
    public string? TpuQuic { get; init; }

    /// <summary>The node's UDP forwarding TPU address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpuForwards")]
    public string? TpuForwards { get; init; }

    /// <summary>The node's QUIC forwarding TPU address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpuForwardsQuic")]
    public string? TpuForwardsQuic { get; init; }

    /// <summary>The node's vote TPU address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpuVote")]
    public string? TpuVote { get; init; }

    /// <summary>The node's repair-service address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("serveRepair")]
    public string? ServeRepair { get; init; }

    /// <summary>The node's JSON-RPC address, or <c>null</c> if it does not serve RPC.</summary>
    [JsonPropertyName("rpc")]
    public string? Rpc { get; init; }

    /// <summary>The node's WebSocket PubSub address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("pubsub")]
    public string? Pubsub { get; init; }

    /// <summary>The node's software version, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>The validator client identifier, or <c>null</c> if the node did not report one.</summary>
    [JsonPropertyName("clientId")]
    public string? ClientId { get; init; }

    /// <summary>The node's feature set id, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("featureSet")]
    public uint? FeatureSet { get; init; }

    /// <summary>The node's shred version, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("shredVersion")]
    public ushort? ShredVersion { get; init; }
}
