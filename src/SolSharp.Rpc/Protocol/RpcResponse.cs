using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 response envelope.</summary>
internal sealed record RpcResponse
{
    /// <summary>The JSON-RPC protocol version.</summary>
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; init; }

    /// <summary>The request identifier echoed by the server.</summary>
    [JsonPropertyName("id")]
    public int? Id { get; init; }

    /// <summary>The raw successful result, or an undefined element when the property was absent.</summary>
    [JsonPropertyName("result")]
    public JsonElement Result { get; init; }

    /// <summary>Whether the response contained a result property, including an explicit JSON null.</summary>
    [JsonIgnore]
    public bool HasResult => Result.ValueKind != JsonValueKind.Undefined;

    /// <summary>The error object when the call failed; otherwise <c>null</c>.</summary>
    [JsonPropertyName("error")]
    public RpcError? Error { get; init; }
}
