using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 request envelope.</summary>
public sealed record RpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    // Constant: one request per HTTP call, so the id is never used for correlation.
    [JsonPropertyName("id")]
    public int Id { get; init; } = 1;

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object[] Params { get; init; } = [];
}
