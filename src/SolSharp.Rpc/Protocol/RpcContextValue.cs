using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolSharp.Rpc.Protocol;

/// <summary>The { context, value } shape many Solana RPC methods wrap their result in.</summary>
/// <typeparam name="T">The type of the wrapped <see cref="Value"/>.</typeparam>
public sealed record RpcContextValue<T>
{
    private RpcContext? _context;

    /// <summary>The slot context the result was produced at.</summary>
    [JsonPropertyName("context")]
    [JsonRequired]
    public RpcContext Context
    {
        get => _context ?? throw new InvalidOperationException("The RPC context has not been initialized.");
        init => _context = value ?? throw new JsonException("An RPC context wrapper must carry a non-null context.");
    }

    /// <summary>The method's actual result value.</summary>
    [JsonPropertyName("value")]
    [JsonRequired]
    public T? Value { get; init; }
}

/// <summary>Slot context attached to a context-wrapped RPC result.</summary>
public sealed record RpcContext
{
    /// <summary>The slot at which the data was retrieved.</summary>
    [JsonPropertyName("slot")]
    [JsonRequired]
    public ulong Slot { get; init; }

    /// <summary>The node's RPC API version, when reported.</summary>
    [JsonPropertyName("apiVersion")]
    public string? ApiVersion { get; init; }
}
