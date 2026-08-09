using SolSharp.Core.Primitives;

namespace SolSharp.Rpc;

/// <summary>Options for <see cref="SolanaRpcClient.RequestAirdropWithOptionsAsync(PublicKey, ulong, RequestAirdropOptions, CancellationToken)"/>.</summary>
public sealed record RequestAirdropOptions
{
    /// <summary>The recent blockhash the faucet transaction must use; the node chooses one when <c>null</c>.</summary>
    public string? RecentBlockhash { get; init; }

    /// <summary>The commitment level used to select the bank that creates the faucet transaction.</summary>
    public Commitment? Commitment { get; init; }
}

/// <summary>Options for <see cref="SolanaRpcClient.GetVoteAccountsWithOptionsAsync(GetVoteAccountsOptions, CancellationToken)"/>.</summary>
public sealed record GetVoteAccountsOptions
{
    /// <summary>Return only the validator with this vote-account address; return all validators when <c>null</c>.</summary>
    public PublicKey? VotePublicKey { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Whether delinquent validators with no active stake remain in the response.</summary>
    public bool? KeepUnstakedDelinquents { get; init; }

    /// <summary>The slot distance after which a validator is considered delinquent.</summary>
    public ulong? DelinquentSlotDistance { get; init; }
}

/// <summary>Options for <see cref="SolanaRpcClient.GetLeaderScheduleWithOptionsAsync(GetLeaderScheduleOptions, CancellationToken)"/>.</summary>
public sealed record GetLeaderScheduleOptions
{
    /// <summary>A slot in the epoch to query; the current epoch when <c>null</c>.</summary>
    public ulong? Slot { get; init; }

    /// <summary>Return only this validator identity; return all identities when <c>null</c>.</summary>
    public PublicKey? Identity { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }
}

/// <summary>Options for <see cref="SolanaRpcClient.GetLargestAccountsWithOptionsAsync(GetLargestAccountsOptions, CancellationToken)"/>.</summary>
public sealed record GetLargestAccountsOptions
{
    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Restrict results to circulating or non-circulating accounts; include both when <c>null</c>.</summary>
    public LargestAccountsFilter? Filter { get; init; }

    /// <summary>Whether the node sorts the result by balance; use the node default when <c>null</c>.</summary>
    public bool? SortResults { get; init; }
}

/// <summary>Options for <see cref="SolanaRpcClient.GetSupplyWithOptionsAsync(GetSupplyOptions, CancellationToken)"/>.</summary>
public sealed record GetSupplyOptions
{
    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>
    /// Omits the potentially large non-circulating account-address list when <c>true</c>.
    /// The upstream default is <c>false</c>.
    /// </summary>
    public bool ExcludeNonCirculatingAccountsList { get; init; }
}

/// <summary>Options for <see cref="SolanaRpcClient.GetInflationRewardWithOptionsAsync(IReadOnlyList{PublicKey}, GetInflationRewardOptions, CancellationToken)"/>.</summary>
public sealed record GetInflationRewardOptions
{
    /// <summary>The epoch to query; the previous epoch when <c>null</c>.</summary>
    public ulong? Epoch { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The minimum slot at which the request may be evaluated.</summary>
    public ulong? MinContextSlot { get; init; }
}

/// <summary>The transaction encoding requested from <c>getBlock</c>, <c>getTransaction</c>, or <c>blockSubscribe</c>.</summary>
public enum RpcTransactionEncoding
{
    /// <summary>The legacy <c>binary</c> alias for base58 encoding.</summary>
    Binary,

    /// <summary>Base64-encoded transaction wire bytes.</summary>
    Base64,

    /// <summary>Base58-encoded transaction wire bytes.</summary>
    Base58,

    /// <summary>JSON transaction objects with compiled instructions.</summary>
    Json,

    /// <summary>JSON transaction objects with recognized instructions parsed by the node.</summary>
    JsonParsed
}

/// <summary>The amount of transaction information requested for a block.</summary>
public enum RpcTransactionDetails
{
    /// <summary>Full transactions and execution metadata.</summary>
    Full,

    /// <summary>Transaction signatures only.</summary>
    Signatures,

    /// <summary>No transaction data.</summary>
    None,

    /// <summary>Transaction signatures and account-key metadata without instructions.</summary>
    Accounts
}

/// <summary>
/// Exact upstream <c>getBlock</c> configuration. The configured response shape depends on
/// <see cref="Encoding"/> and <see cref="TransactionDetails"/> and is therefore returned as JSON.
/// </summary>
public sealed record GetBlockOptions
{
    /// <summary>The transaction encoding; use the node default when <c>null</c>.</summary>
    public RpcTransactionEncoding? Encoding { get; init; }

    /// <summary>The transaction detail level; use the node default when <c>null</c>.</summary>
    public RpcTransactionDetails? TransactionDetails { get; init; }

    /// <summary>Whether block-level rewards are included; use the node default when <c>null</c>.</summary>
    public bool? Rewards { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The highest numeric transaction version the caller accepts.</summary>
    public byte? MaxSupportedTransactionVersion { get; init; }
}

/// <summary>
/// Exact upstream <c>getTransaction</c> configuration. The configured response shape depends on
/// <see cref="Encoding"/> and is therefore returned as JSON.
/// </summary>
public sealed record GetTransactionOptions
{
    /// <summary>The transaction encoding; use the node default when <c>null</c>.</summary>
    public RpcTransactionEncoding? Encoding { get; init; }

    /// <summary>The commitment level to query at.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The highest numeric transaction version the caller accepts.</summary>
    public byte? MaxSupportedTransactionVersion { get; init; }
}

internal static class RpcWireNames
{
    public static string AccountEncoding(RpcAccountEncoding value) => value switch
    {
        RpcAccountEncoding.Binary => "binary",
        RpcAccountEncoding.Base58 => "base58",
        RpcAccountEncoding.Base64 => "base64",
        RpcAccountEncoding.JsonParsed => "jsonParsed",
        RpcAccountEncoding.Base64Zstd => "base64+zstd",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown account encoding.")
    };

    public static bool TryAccountEncoding(string? value, out RpcAccountEncoding encoding)
    {
        encoding = value switch
        {
            "binary" => RpcAccountEncoding.Binary,
            "base58" => RpcAccountEncoding.Base58,
            "base64" => RpcAccountEncoding.Base64,
            "jsonParsed" => RpcAccountEncoding.JsonParsed,
            "base64+zstd" => RpcAccountEncoding.Base64Zstd,
            _ => default
        };

        return value is "binary" or "base58" or "base64" or "jsonParsed" or "base64+zstd";
    }

    public static string TransactionEncoding(RpcTransactionEncoding value) => value switch
    {
        RpcTransactionEncoding.Binary => "binary",
        RpcTransactionEncoding.Base64 => "base64",
        RpcTransactionEncoding.Base58 => "base58",
        RpcTransactionEncoding.Json => "json",
        RpcTransactionEncoding.JsonParsed => "jsonParsed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction encoding.")
    };

    public static string TransactionDetails(RpcTransactionDetails value) => value switch
    {
        RpcTransactionDetails.Full => "full",
        RpcTransactionDetails.Signatures => "signatures",
        RpcTransactionDetails.None => "none",
        RpcTransactionDetails.Accounts => "accounts",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown transaction detail level.")
    };
}
