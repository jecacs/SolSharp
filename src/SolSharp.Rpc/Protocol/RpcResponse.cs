using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 response envelope.</summary>
/// <typeparam name="T">The type of the <see cref="Result"/> payload.</typeparam>
internal sealed record RpcResponse<T>
{
    /// <summary>The successful result, or <c>null</c> when the call returned an error.</summary>
    [JsonPropertyName("result")]
    public T? Result { get; init; }

    /// <summary>The error object when the call failed; otherwise <c>null</c>.</summary>
    [JsonPropertyName("error")]
    public RpcError? Error { get; init; }
}
