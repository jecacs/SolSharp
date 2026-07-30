using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.SendTransactionAsync"/>.</summary>
public sealed record SendTransactionOptions
{
    /// <summary>Skips the preflight transaction checks (simulation) before submitting. Default <c>false</c>.</summary>
    public bool SkipPreflight { get; init; }

    /// <summary>
    /// The commitment preflight runs at. Defaults to <see cref="Commitment.Confirmed"/> to match
    /// <see cref="SolanaRpcClient.GetLatestBlockhashAsync"/>: the node's own default is <c>finalized</c>,
    /// where a blockhash fetched at <c>confirmed</c> may not exist yet, so preflight would report
    /// <c>BlockhashNotFound</c> for a perfectly valid transaction. Set to <c>null</c> for the node default.
    /// </summary>
    public Commitment? PreflightCommitment { get; init; } = Core.Primitives.Commitment.Confirmed;

    /// <summary>The maximum number of times the node retries forwarding the transaction. The node default is used when <c>null</c>.</summary>
    public uint? MaxRetries { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}
