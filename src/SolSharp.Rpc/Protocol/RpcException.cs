namespace SolSharp.Rpc.Protocol;

/// <summary>Thrown when the node returns a JSON-RPC error.</summary>
public sealed class RpcException(int code, string message) : Exception($"RPC error {code}: {message}")
{
    /// <summary>The JSON-RPC error code returned by the node.</summary>
    public int Code { get; } = code;
}
