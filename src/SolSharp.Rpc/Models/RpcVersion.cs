using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The node's software version, as returned by <c>getVersion</c>.</summary>
public sealed record RpcVersion
{
    /// <summary>The solana-core software version string (for example <c>"1.18.0"</c>).</summary>
    [JsonPropertyName("solana-core")]
    public string SolanaCore { get; init; } = string.Empty;

    /// <summary>The numeric feature set the node has enabled, if reported.</summary>
    [JsonPropertyName("feature-set")]
    public long? FeatureSet { get; init; }
}
