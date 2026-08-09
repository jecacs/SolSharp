using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>
/// Commitment and minimum-context-slot options shared by RPC methods backed by Agave's
/// <c>RpcContextConfig</c>. Unset fields use the node defaults.
/// </summary>
public sealed record RpcContextOptions
{
    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}
