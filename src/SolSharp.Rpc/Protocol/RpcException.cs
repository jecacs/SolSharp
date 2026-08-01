using System.Text.Json;

namespace SolSharp.Rpc.Protocol;

/// <summary>Thrown when the node returns a JSON-RPC error.</summary>
public sealed class RpcException(int code, string message) : Exception($"RPC error {code}: {message}")
{
    /// <summary>Creates an exception and preserves the node's structured error data.</summary>
    /// <param name="code">The JSON-RPC error code.</param>
    /// <param name="message">The node's human-readable message.</param>
    /// <param name="errorData">The optional JSON-RPC <c>error.data</c> value.</param>
    internal RpcException(int code, string message, JsonElement? errorData)
        : this(code, message)
    {
        ErrorData = errorData?.Clone();
    }

    /// <summary>The JSON-RPC error code returned by the node.</summary>
    public int Code { get; } = code;

    /// <summary>The optional structured JSON-RPC <c>error.data</c> diagnostics returned by the node.</summary>
    public JsonElement? ErrorData { get; }
}
