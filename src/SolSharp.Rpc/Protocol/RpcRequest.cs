using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>A JSON-RPC 2.0 request envelope.</summary>
public sealed record RpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    // Defaults to 1 for one-shot HTTP, where correlation is by the round-trip. The WebSocket
    // client overrides it with a unique id per request, where correlation does matter.
    [JsonPropertyName("id")]
    public int Id { get; init; } = 1;

    [JsonPropertyName("method")]
    public required string Method { get; init; }

    [JsonPropertyName("params")]
    public object[] Params { get; init; } = [];
}
