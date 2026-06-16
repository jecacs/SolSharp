using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.SimulateTransactionAsync"/>.</summary>
public sealed record SimulateTransactionOptions
{
    /// <summary>Verifies the transaction's signatures during simulation. Default <c>false</c>; cannot be combined with <see cref="ReplaceRecentBlockhash"/>.</summary>
    public bool SigVerify { get; init; }

    /// <summary>Replaces the transaction's recent blockhash with the latest one before simulating. Default <c>false</c>.</summary>
    public bool ReplaceRecentBlockhash { get; init; }

    /// <summary>The commitment the simulation runs at. The node default is used when <c>null</c>.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}
