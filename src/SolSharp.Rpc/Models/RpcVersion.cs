using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Models;

public sealed record RpcVersion
{
    [JsonPropertyName("solana-core")]
    public string SolanaCore { get; init; } = string.Empty;

    [JsonPropertyName("feature-set")]
    public long? FeatureSet { get; init; }
}
