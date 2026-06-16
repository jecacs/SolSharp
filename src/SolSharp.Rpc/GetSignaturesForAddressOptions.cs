using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.GetSignaturesForAddressAsync"/>; unset fields fall back to the node defaults.</summary>
public sealed record GetSignaturesForAddressOptions
{
    /// <summary>The maximum number of signatures to return (1-1000); the node defaults to 1000 when null.</summary>
    public int? Limit { get; init; }

    /// <summary>Start searching backwards from this signature (exclusive).</summary>
    public string? Before { get; init; }

    /// <summary>Search backwards only until this signature is reached (exclusive).</summary>
    public string? Until { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The minimum slot the request can be evaluated at.</summary>
    public ulong? MinContextSlot { get; init; }
}
