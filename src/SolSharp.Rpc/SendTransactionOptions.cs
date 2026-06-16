using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.SendTransactionAsync"/>.</summary>
public sealed record SendTransactionOptions
{
    /// <summary>Skips the preflight transaction checks (simulation) before submitting. Default <c>false</c>.</summary>
    public bool SkipPreflight { get; init; }

    /// <summary>The commitment preflight runs at. The node default is used when <c>null</c>.</summary>
    public Commitment? PreflightCommitment { get; init; }

    /// <summary>The maximum number of times the node retries forwarding the transaction. The node default is used when <c>null</c>.</summary>
    public uint? MaxRetries { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}
