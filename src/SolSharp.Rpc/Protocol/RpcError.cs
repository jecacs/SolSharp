using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC error object.</summary>
public sealed record RpcError
{
    /// <summary>The JSON-RPC error code.</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; }

    /// <summary>The human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}
