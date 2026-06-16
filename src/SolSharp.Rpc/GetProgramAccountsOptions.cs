using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.GetProgramAccountsAsync"/>; unset fields fall back to the node defaults.</summary>
public sealed record GetProgramAccountsOptions
{
    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Filters every returned account must satisfy (memcmp / data size); none are applied when null.</summary>
    public IReadOnlyList<AccountFilter>? Filters { get; init; }

    /// <summary>The minimum slot the request can be evaluated at.</summary>
    public ulong? MinContextSlot { get; init; }
}
