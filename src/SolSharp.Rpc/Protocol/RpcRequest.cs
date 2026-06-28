using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 request envelope.</summary>
internal sealed record RpcRequest
{
    /// <summary>The JSON-RPC protocol version; always <c>"2.0"</c>.</summary>
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>The request id echoed back in the matching response.</summary>
    [JsonPropertyName("id")]
    public int Id { get; init; } = 1;

    /// <summary>The RPC method name (for example <c>"getBalance"</c>).</summary>
    [JsonPropertyName("method")]
    public required string Method { get; init; }

    /// <summary>The positional parameters for the method, in order.</summary>
    [JsonPropertyName("params")]
    public object[] Params { get; init; } = [];
}
