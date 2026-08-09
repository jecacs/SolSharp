using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>The account-data encoding accepted by Solana account RPC methods.</summary>
public enum RpcAccountEncoding
{
    /// <summary>The legacy bare base58 string response.</summary>
    Binary,

    /// <summary>A base58 string paired with the <c>base58</c> encoding tag.</summary>
    Base58,

    /// <summary>A base64 string paired with the <c>base64</c> encoding tag.</summary>
    Base64,

    /// <summary>
    /// A node-parsed object when the account owner is recognized, with a base64 tuple fallback otherwise.
    /// </summary>
    JsonParsed,

    /// <summary>A zstd-compressed byte sequence encoded as base64 and tagged <c>base64+zstd</c>.</summary>
    Base64Zstd
}

/// <summary>
/// Exact upstream account configuration for <c>getAccountInfo</c>, <c>getMultipleAccounts</c>, and
/// token-account scans. Unset fields use the node defaults.
/// </summary>
public sealed record RpcAccountInfoOptions
{
    /// <summary>The account-data encoding; use the method-specific node default when <c>null</c>.</summary>
    public RpcAccountEncoding? Encoding { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Return only this slice of the account data; return all data when <c>null</c>.</summary>
    public DataSlice? DataSlice { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}

/// <summary>
/// Exact upstream <c>getProgramAccounts</c> configuration. The response data remains typed even though
/// <see cref="Encoding"/> changes the account-data branch.
/// </summary>
public sealed record RpcProgramAccountsOptions
{
    /// <summary>The account-data encoding; use the node default when <c>null</c>.</summary>
    public RpcAccountEncoding? Encoding { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Filters every returned account must satisfy; apply none when <c>null</c>.</summary>
    public IReadOnlyList<AccountFilter>? Filters { get; init; }

    /// <summary>Return only this slice of each account's data; return all data when <c>null</c>.</summary>
    public DataSlice? DataSlice { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }

    /// <summary>
    /// Request the upstream <c>{ context, value }</c> response shape when <c>true</c>. The list-returning
    /// client method unwraps its <c>value</c> component.
    /// </summary>
    public bool? WithContext { get; init; }

    /// <summary>Whether the node sorts accounts by public key; use the node default when <c>null</c>.</summary>
    public bool? SortResults { get; init; }
}
