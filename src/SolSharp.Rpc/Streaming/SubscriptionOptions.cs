using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Streaming;

internal enum LogsSubscriptionFilterKind
{
    /// <summary>All non-vote transactions.</summary>
    All,

    /// <summary>All transactions, including votes.</summary>
    AllWithVotes,

    /// <summary>Transactions mentioning one address.</summary>
    Mentions
}

/// <summary>
/// Effective upstream <c>accountSubscribe</c> configuration. Pinned Agave ignores the other fields present on
/// its shared HTTP account configuration, so they are intentionally not exposed here.
/// </summary>
public sealed record AccountSubscriptionOptions
{
    /// <summary>The account-data encoding; use the node's legacy binary default when <c>null</c>.</summary>
    public RpcAccountEncoding? Encoding { get; init; }

    /// <summary>The commitment level at which account changes are delivered.</summary>
    public Commitment? Commitment { get; init; }
}

/// <summary>
/// Effective upstream <c>programSubscribe</c> configuration. Pinned Agave applies encoding, commitment, and
/// filters; its data-slice, context-shape, and sorting fields are accepted but ignored and are not exposed.
/// </summary>
public sealed record ProgramSubscriptionOptions
{
    /// <summary>The account-data encoding; use the node's legacy binary default when <c>null</c>.</summary>
    public RpcAccountEncoding? Encoding { get; init; }

    /// <summary>The commitment level at which program-account changes are delivered.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>Filters every delivered account must satisfy; apply none when <c>null</c>.</summary>
    public IReadOnlyList<AccountFilter>? Filters { get; init; }
}

/// <summary>Options for <c>signatureSubscribe</c>.</summary>
public sealed record SignatureSubscriptionOptions
{
    /// <summary>The commitment level at which the final notification is delivered.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>
    /// Requests an early <c>"receivedSignature"</c> notification before the final processed result.
    /// </summary>
    public bool EnableReceivedNotification { get; init; }
}

/// <summary>The filter accepted by <c>logsSubscribe</c>.</summary>
public sealed class LogsSubscriptionFilter
{
    private LogsSubscriptionFilter(LogsSubscriptionFilterKind kind, PublicKey? mention)
    {
        Kind = kind;
        Mention = mention;
    }

    /// <summary>Includes all non-vote transactions.</summary>
    public static LogsSubscriptionFilter All { get; } = new(LogsSubscriptionFilterKind.All, null);

    /// <summary>Includes all transactions, including simple vote transactions.</summary>
    public static LogsSubscriptionFilter AllWithVotes { get; } = new(LogsSubscriptionFilterKind.AllWithVotes, null);

    internal LogsSubscriptionFilterKind Kind { get; }

    internal PublicKey? Mention { get; }

    /// <summary>Includes transactions that mention one account or program.</summary>
    /// <param name="accountOrProgram">The single address to match.</param>
    /// <returns>A mentions filter.</returns>
    public static LogsSubscriptionFilter Mentions(PublicKey accountOrProgram) =>
        new(LogsSubscriptionFilterKind.Mentions, accountOrProgram);
}

/// <summary>The filter accepted by <c>blockSubscribe</c>.</summary>
public sealed class BlockSubscriptionFilter
{
    private BlockSubscriptionFilter(PublicKey? mention) => Mention = mention;

    /// <summary>Includes every produced block.</summary>
    public static BlockSubscriptionFilter All { get; } = new(null);

    internal PublicKey? Mention { get; }

    /// <summary>Includes blocks that mention one account or program.</summary>
    /// <param name="accountOrProgram">The address a block must mention.</param>
    /// <returns>A mentions filter.</returns>
    public static BlockSubscriptionFilter Mentions(PublicKey accountOrProgram) => new(accountOrProgram);
}

/// <summary>
/// Exact upstream <c>blockSubscribe</c> configuration. The block body is returned as JSON because its
/// schema depends on the encoding and transaction-detail choices.
/// </summary>
public sealed record BlockSubscriptionOptions
{
    /// <summary>The commitment level at which blocks are delivered.</summary>
    public Commitment? Commitment { get; init; }

    /// <summary>The transaction encoding; use the node default when <c>null</c>.</summary>
    public RpcTransactionEncoding? Encoding { get; init; }

    /// <summary>The transaction detail level; use the node default when <c>null</c>.</summary>
    public RpcTransactionDetails? TransactionDetails { get; init; }

    /// <summary>Whether block-level rewards are included; use the node default when <c>null</c>.</summary>
    public bool? ShowRewards { get; init; }

    /// <summary>The highest numeric transaction version the caller accepts.</summary>
    public byte? MaxSupportedTransactionVersion { get; init; }
}
