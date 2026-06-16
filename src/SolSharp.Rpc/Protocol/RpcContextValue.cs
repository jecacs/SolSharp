using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>The { context, value } shape many Solana RPC methods wrap their result in.</summary>
public sealed record RpcContextValue<T>
{
    [JsonPropertyName("context")]
    public RpcContext? Context { get; init; }

    [JsonPropertyName("value")]
    public T? Value { get; init; }
}

/// <summary>Slot context attached to a context-wrapped RPC result.</summary>
public sealed record RpcContext
{
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }
}
