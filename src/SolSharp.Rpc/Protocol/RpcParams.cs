using System.Text.Json.Serialization;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Protocol;

// The typed wire shapes of the JSON-RPC parameter objects. These used to be anonymous types, but the
// source-generated SolanaJsonContext cannot register anonymous types, so each shape is a named internal
// record instead. Property order mirrors the previous anonymous-type declarations byte for byte, and
// null-valued optionals are dropped by the shared WhenWritingNull policy exactly as before.

/// <summary>The <c>{ commitment }</c> configuration object most read methods take as their trailing parameter.</summary>
internal sealed record CommitmentConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }
}

/// <summary>The <c>sendTransaction</c> configuration object.</summary>
internal sealed record SendTransactionConfig
{
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("skipPreflight")]
    public required bool SkipPreflight { get; init; }

    [JsonPropertyName("preflightCommitment")]
    public Commitment? PreflightCommitment { get; init; }

    [JsonPropertyName("maxRetries")]
    public uint? MaxRetries { get; init; }

    [JsonPropertyName("minContextSlot")]
    public ulong? MinContextSlot { get; init; }
}

/// <summary>The <c>simulateTransaction</c> configuration object.</summary>
internal sealed record SimulateTransactionConfig
{
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("sigVerify")]
    public required bool SigVerify { get; init; }

    [JsonPropertyName("replaceRecentBlockhash")]
    public required bool ReplaceRecentBlockhash { get; init; }

    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("minContextSlot")]
    public ulong? MinContextSlot { get; init; }

    [JsonPropertyName("accounts")]
    public SimulateTransactionAccountsConfig? Accounts { get; init; }

    [JsonPropertyName("innerInstructions")]
    public bool? InnerInstructions { get; init; }
}

/// <summary>The optional post-simulation accounts requested from <c>simulateTransaction</c>.</summary>
internal sealed record SimulateTransactionAccountsConfig
{
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("addresses")]
    public required PublicKey[] Addresses { get; init; }
}

/// <summary>
/// The <c>{ encoding, commitment, dataSlice }</c> configuration object of <c>getAccountInfo</c>,
/// <c>getMultipleAccounts</c>, <c>getTokenAccountsByOwner</c>, and <c>accountSubscribe</c> (the latter
/// three simply leave <see cref="DataSlice"/> unset).
/// </summary>
internal sealed record AccountInfoConfig
{
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("dataSlice")]
    public DataSlice? DataSlice { get; init; }
}

/// <summary>The <c>getSignaturesForAddress</c> configuration object.</summary>
internal sealed record SignaturesForAddressConfig
{
    [JsonPropertyName("limit")]
    public int? Limit { get; init; }

    [JsonPropertyName("before")]
    public string? Before { get; init; }

    [JsonPropertyName("until")]
    public string? Until { get; init; }

    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("minContextSlot")]
    public ulong? MinContextSlot { get; init; }
}

/// <summary>
/// The <c>getProgramAccounts</c> / <c>programSubscribe</c> configuration object (the subscription leaves
/// <see cref="MinContextSlot"/> and <see cref="DataSlice"/> unset). Filter entries are the payload records
/// built by <see cref="AccountFilter"/>.
/// </summary>
internal sealed record ProgramAccountsConfig
{
    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("minContextSlot")]
    public ulong? MinContextSlot { get; init; }

    [JsonPropertyName("dataSlice")]
    public DataSlice? DataSlice { get; init; }

    [JsonPropertyName("filters")]
    public object[]? Filters { get; init; }
}

/// <summary>The <c>{ mint }</c> filter of <c>getTokenAccountsByOwner</c>.</summary>
internal sealed record MintFilter
{
    [JsonPropertyName("mint")]
    public required PublicKey Mint { get; init; }
}

/// <summary>The <c>getTransaction</c> configuration object (base64 or jsonParsed encoding).</summary>
internal sealed record TransactionConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("maxSupportedTransactionVersion")]
    public required int MaxSupportedTransactionVersion { get; init; }

    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }
}

/// <summary>The <c>getBlock</c> configuration object (<see cref="Encoding"/> is unset for the signatures-only read).</summary>
internal sealed record BlockConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("maxSupportedTransactionVersion")]
    public required int MaxSupportedTransactionVersion { get; init; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }

    [JsonPropertyName("transactionDetails")]
    public required string TransactionDetails { get; init; }

    [JsonPropertyName("rewards")]
    public required bool Rewards { get; init; }
}

/// <summary>The <c>getSupply</c> configuration object.</summary>
internal sealed record SupplyConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("excludeNonCirculatingAccountsList")]
    public required bool ExcludeNonCirculatingAccountsList { get; init; }
}

/// <summary>The <c>getSignatureStatuses</c> configuration object.</summary>
internal sealed record SignatureStatusesConfig
{
    [JsonPropertyName("searchTransactionHistory")]
    public required bool SearchTransactionHistory { get; init; }
}

/// <summary>The <c>getInflationReward</c> configuration object.</summary>
internal sealed record InflationRewardConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("epoch")]
    public ulong? Epoch { get; init; }
}

/// <summary>The <c>{ mentions }</c> filter of <c>logsSubscribe</c>.</summary>
internal sealed record LogsFilter
{
    [JsonPropertyName("mentions")]
    public required PublicKey[] Mentions { get; init; }
}

/// <summary>The <c>{ mentionsAccountOrProgram }</c> filter of <c>blockSubscribe</c>.</summary>
internal sealed record BlockSubscribeFilter
{
    [JsonPropertyName("mentionsAccountOrProgram")]
    public required PublicKey MentionsAccountOrProgram { get; init; }
}

/// <summary>The <c>getLargestAccounts</c> configuration object.</summary>
internal sealed record LargestAccountsConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("filter")]
    public string? Filter { get; init; }
}

/// <summary>The <c>getBlockProduction</c> configuration object.</summary>
internal sealed record BlockProductionConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("identity")]
    public PublicKey? Identity { get; init; }

    [JsonPropertyName("range")]
    public SlotRangeConfig? Range { get; init; }
}

/// <summary>The slot range of a <c>getBlockProduction</c> query; the node defaults to the current epoch.</summary>
internal sealed record SlotRangeConfig
{
    [JsonPropertyName("firstSlot")]
    public required ulong FirstSlot { get; init; }

    [JsonPropertyName("lastSlot")]
    public ulong? LastSlot { get; init; }
}

/// <summary>The <c>blockSubscribe</c> configuration object (note: <c>showRewards</c>, unlike <c>getBlock</c>'s <c>rewards</c>).</summary>
internal sealed record BlockSubscribeConfig
{
    [JsonPropertyName("commitment")]
    public Commitment? Commitment { get; init; }

    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }

    [JsonPropertyName("transactionDetails")]
    public required string TransactionDetails { get; init; }

    [JsonPropertyName("showRewards")]
    public required bool ShowRewards { get; init; }

    [JsonPropertyName("maxSupportedTransactionVersion")]
    public required int MaxSupportedTransactionVersion { get; init; }
}
