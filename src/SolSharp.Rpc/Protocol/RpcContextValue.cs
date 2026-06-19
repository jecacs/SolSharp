using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>The { context, value } shape many Solana RPC methods wrap their result in.</summary>
/// <typeparam name="T">The type of the wrapped <see cref="Value"/>.</typeparam>
public sealed record RpcContextValue<T>
{
    /// <summary>The slot context the result was produced at.</summary>
    [JsonPropertyName("context")]
    public RpcContext? Context { get; init; }

    /// <summary>The method's actual result value.</summary>
    [JsonPropertyName("value")]
    public T? Value { get; init; }
}

/// <summary>Slot context attached to a context-wrapped RPC result.</summary>
public sealed record RpcContext
{
    /// <summary>The slot at which the data was retrieved.</summary>
    [JsonPropertyName("slot")]
    public ulong Slot { get; init; }
}
