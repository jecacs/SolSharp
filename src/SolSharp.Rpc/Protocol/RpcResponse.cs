using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 response envelope.</summary>
public sealed record RpcResponse<T>
{
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    [JsonPropertyName("error")]
    public RpcError? Error { get; init; }
}
