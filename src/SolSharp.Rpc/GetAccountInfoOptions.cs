using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>
/// Options for account-info RPC reads, including <c>getAccountInfo</c>,
/// <c>getMultipleAccounts</c>, and token-account scans. Unset fields use the node defaults.
/// </summary>
public sealed record GetAccountInfoOptions
{
    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Return only this slice of each account's data; return all data when <c>null</c>.</summary>
    public DataSlice? DataSlice { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}
