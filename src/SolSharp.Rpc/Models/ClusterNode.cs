using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>A node participating in the cluster, as returned by <c>getClusterNodes</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getclusternodes">getClusterNodes</seealso>
public sealed record ClusterNode
{
    /// <summary>The node's identity public key.</summary>
    [JsonPropertyName("pubkey")]
    public PublicKey Pubkey { get; init; }

    /// <summary>The node's gossip network address (host:port), or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("gossip")]
    public string? Gossip { get; init; }

    /// <summary>The node's TPU (transaction processing unit) address, or <c>null</c> if unavailable.</summary>
    [JsonPropertyName("tpu")]
    public string? Tpu { get; init; }

    /// <summary>The node's JSON-RPC address, or <c>null</c> if it does not serve RPC.</summary>
    [JsonPropertyName("rpc")]
    public string? Rpc { get; init; }

    /// <summary>The node's software version, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>The node's feature set id, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("featureSet")]
    public long? FeatureSet { get; init; }

    /// <summary>The node's shred version, or <c>null</c> if unknown.</summary>
    [JsonPropertyName("shredVersion")]
    public int? ShredVersion { get; init; }
}
