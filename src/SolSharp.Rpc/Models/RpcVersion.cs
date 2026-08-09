using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

/// <summary>The node's software version, as returned by <c>getVersion</c>.</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getversion">getVersion</seealso>
public sealed record RpcVersion
{
    private string? _solanaCore;

    /// <summary>The solana-core software version string (for example <c>"1.18.0"</c>).</summary>
    [JsonPropertyName("solana-core")]
    [JsonRequired]
    public string SolanaCore
    {
        get => _solanaCore ?? throw new InvalidOperationException("The node version has not been initialized.");
        init => _solanaCore = value ?? throw new JsonException("A version response must carry solana-core.");
    }

    /// <summary>The numeric feature set the node has enabled, if reported.</summary>
    [JsonPropertyName("feature-set")]
    public uint? FeatureSet { get; init; }
}
