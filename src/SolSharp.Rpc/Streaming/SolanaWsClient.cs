using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Parsed;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A multiplexed Solana WebSocket client: every subscription shares one connection and
/// notifications are routed by subscription id. Subscriptions are exposed either as an
/// <see cref="IAsyncEnumerable{T}"/> (which unsubscribes when enumeration ends) or as a
/// <see cref="System.Threading.Channels.ChannelReader{T}"/> (which unsubscribes when its token is cancelled).
/// When <see cref="SolanaWsClientOptions.AutoReconnect"/> is enabled (the default), a dropped connection
/// is transparently re-established and the active subscriptions are replayed onto it.
/// A notification that fails to decode faults only its own subscription; the connection and the other
/// subscriptions are unaffected.
/// </summary>
public sealed class SolanaWsClient : IAsyncDisposable
{
    private static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private readonly Func<IWebSocketConnection> _connectionFactory;
    private readonly SolanaWsClientOptions _options;
    private readonly Lock _stateGate = new();
    private readonly Dictionary<int, PendingSubscribe> _pending = [];
    private readonly Dictionary<long, Subscription> _active = [];
    private readonly Dictionary<(long Generation, ulong ServerId), RouteGroup> _byServerId = [];
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ILogger _logger;
    private readonly NotificationBufferBudget _notificationBufferBudget;

    private ConnectionEpoch? _connection;
    private ConnectionEpoch? _connecting;
    private Uri? _endpoint;
    private int _nextRequestId;
    private long _nextLocalId;
    private long _connectionGeneration;
    private Task? _runLoop;
    private Task? _connectTask;
    private Task? _disposeTask;
    private ClientPhase _phase;
    private int _sendOperationCount;
    private TaskCompletionSource? _sendOperationsDrained;
    private int _cancellationRegistrationCount;

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/> with default options.</summary>
    /// <param name="loggerFactory">Optional factory for connection/reconnection diagnostics; no logging when null.</param>
    public SolanaWsClient(ILoggerFactory? loggerFactory = null) : this(new(), loggerFactory)
    {
    }

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/>.</summary>
    /// <param name="options">Connection, buffering, and auto-reconnect options.</param>
    /// <param name="loggerFactory">Optional factory for connection/reconnection diagnostics; no logging when null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size, capacity, or timeout option is invalid.</exception>
    public SolanaWsClient(SolanaWsClientOptions options, ILoggerFactory? loggerFactory = null)
        : this(() => new ClientWebSocketConnection(options.MaxMessageSizeBytes), options, loggerFactory)
    {
    }

    internal SolanaWsClient(Func<IWebSocketConnection> connectionFactory, SolanaWsClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxMessageSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum message size must be positive.");
        if (options.SubscriptionBufferCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Subscription buffer capacity must be positive.");
        if (options.MaxBufferedNotificationBytesPerSubscription <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Per-subscription notification byte limit must be positive.");
        if (options.MaxBufferedNotificationBytesTotal <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Total notification byte limit must be positive.");

        // A per-subscription limit above the total can never be reached, so every overflow would be
        // reported as a client-wide one and the per-subscription limit would silently mean nothing.
        if (options.MaxBufferedNotificationBytesPerSubscription > options.MaxBufferedNotificationBytesTotal)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Per-subscription notification byte limit cannot exceed the total notification byte limit.");
        }

        if (options.ReconnectInitialDelay < TimeSpan.Zero || options.ReconnectInitialDelay > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Initial reconnect delay must be non-negative and supported by a timer.");
        if (options.ReconnectMaxDelay < options.ReconnectInitialDelay || options.ReconnectMaxDelay > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum reconnect delay must be at least the initial delay and supported by a timer.");
        if (options.MaxReconnectAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum reconnect attempts cannot be negative.");
        if (options.SubscriptionAckTimeout <= TimeSpan.Zero || options.SubscriptionAckTimeout > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Subscription acknowledgement timeout must be positive and finite.");
        if (options.MaxPendingSubscriptionRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum pending subscription requests must be positive.");
        if (options.ReceiveTimeout != Timeout.InfiniteTimeSpan && options.ReceiveTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Receive timeout must be positive or infinite.");
        if (options.ReceiveTimeout > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Receive timeout is too large for a timer.");

        _connectionFactory = connectionFactory;
        _options = options;
        _logger = loggerFactory?.CreateLogger<SolanaWsClient>() ?? NullLogger<SolanaWsClient>.Instance;
        _notificationBufferBudget = new(options.MaxBufferedNotificationBytesTotal);
    }

    internal SolanaWsClient(IWebSocketConnection connection)
        : this(() => connection, new() { AutoReconnect = false })
    {
    }

    private enum ClientPhase
    {
        New,
        Connecting,
        Connected,
        Reconnecting,
        Stopped,
        Disposing,
        Disposed
    }

    private enum SubscriptionPhase
    {
        Establishing,
        Active,
        Terminal
    }

    private enum OneShotBehavior
    {
        None,
        SignatureFinal
    }

    private enum PendingState
    {
        Awaiting,
        Abandoned,
        Acknowledged,
        LateAcknowledged,
        Failed
    }

    private enum RouteState
    {
        Active,
        Closing,
        Unsubscribing
    }

    private enum BufferReservationResult
    {
        Reserved,
        Completed,
        SubscriptionLimitExceeded,
        TotalLimitExceeded
    }

    private interface ISubscriptionSink
    {
        void Deliver(JsonElement result, int encodedMessageSizeBytes);

        void Complete(Exception? exception);
    }

    internal int RetainedCancellationRegistrationCount
    {
        get
        {
            lock (_stateGate)
                return _cancellationRegistrationCount;
        }
    }

    internal int RetainedPendingSubscriptionReferenceCount
    {
        get
        {
            lock (_stateGate)
                return _pending.Values.Sum(static pending => pending.Subscriptions.Count);
        }
    }

    internal int RetainedAcknowledgementTombstoneCount
    {
        get
        {
            lock (_stateGate)
                return _pending.Values.Count(static pending => pending.State == PendingState.Abandoned);
        }
    }

    internal int RetainedSendOperationCount
    {
        get
        {
            lock (_stateGate)
                return _sendOperationCount;
        }
    }

    internal int RetainedServerRouteCount
    {
        get
        {
            lock (_stateGate)
                return _byServerId.Count;
        }
    }

    internal long RetainedBufferedNotificationBytes => _notificationBufferBudget.BufferedBytes;

    /// <summary>
    /// Opens the WebSocket connection and starts the receive loop. The loop runs until the client is
    /// disposed; with auto-reconnect enabled it also survives transient disconnects.
    /// </summary>
    /// <param name="endpoint">The WebSocket endpoint (wss://...).</param>
    /// <param name="cancellationToken">A token to cancel the initial connect.</param>
    /// <returns>A task that completes once connected.</returns>
    /// <exception cref="System.Net.WebSockets.WebSocketException">The connection could not be established.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="InvalidOperationException">The client is already connected.</exception>
    /// <exception cref="ObjectDisposedException">The client has been disposed.</exception>
    public Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        TaskCompletionSource completion;
        lock (_stateGate)
        {
            if (_phase is ClientPhase.Disposing or ClientPhase.Disposed)
                return Task.FromException(new ObjectDisposedException(nameof(SolanaWsClient)));
            if (_phase != ClientPhase.New)
                return Task.FromException(
                    new InvalidOperationException("The client is already connected; create one client per connection."));

            _phase = ClientPhase.Connecting;
            _endpoint = endpoint;
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _connectTask = completion.Task;
        }

        _ = ConnectInitialAsync(endpoint, completion, cancellationToken);
        return completion.Task;
    }

    /// <summary>
    /// Subscribes to slot-change notifications. Ending the enumeration sends the matching unsubscribe.
    /// See <see href="https://solana.com/docs/rpc/websocket/slotsubscribe">slotSubscribe</see>.
    /// </summary>
    /// <param name="cancellationToken">Stops the subscription when cancelled.</param>
    /// <returns>An async stream of slot notifications.</returns>
    /// <exception cref="InvalidOperationException">Surfaced during enumeration if the connection closes or the node rejects the subscription.</exception>
    public IAsyncEnumerable<SlotInfo> SubscribeSlotsAsync(CancellationToken cancellationToken = default)
        => SubscribeAsync<SlotInfo>("slotSubscribe", [], "slotUnsubscribe", cancellationToken);

    /// <summary>
    /// Subscribes to root-change notifications - the slot the cluster has newly rooted (finalized). Ending the
    /// enumeration sends the matching unsubscribe.
    /// See <see href="https://solana.com/docs/rpc/websocket/rootsubscribe">rootSubscribe</see>.
    /// </summary>
    /// <param name="cancellationToken">Stops the subscription when cancelled.</param>
    /// <returns>An async stream of newly rooted slot numbers.</returns>
    /// <exception cref="InvalidOperationException">Surfaced during enumeration if the connection closes or the node rejects the subscription.</exception>
    public IAsyncEnumerable<ulong> SubscribeRootsAsync(CancellationToken cancellationToken = default)
        => SubscribeAsync<ulong>("rootSubscribe", [], "rootUnsubscribe", cancellationToken);

    /// <summary>
    /// Subscribes to new votes observed in gossip, before they land in a block. Ending the enumeration
    /// sends the matching unsubscribe. This subscription is marked unstable by Solana and is only
    /// available on nodes started with <c>--rpc-pubsub-enable-vote-subscription</c>; on other nodes the
    /// subscribe call is rejected.
    /// See <see href="https://solana.com/docs/rpc/websocket/votesubscribe">voteSubscribe</see>.
    /// </summary>
    /// <param name="cancellationToken">Stops the subscription when cancelled.</param>
    /// <returns>An async stream of vote notifications.</returns>
    /// <exception cref="InvalidOperationException">Surfaced during enumeration if the connection closes or the node rejects the subscription (for example, when vote subscriptions are not enabled).</exception>
    public IAsyncEnumerable<VoteNotification> SubscribeVotesAsync(CancellationToken cancellationToken = default)
        => SubscribeAsync<VoteNotification>("voteSubscribe", [], "voteUnsubscribe", cancellationToken);

    /// <summary>
    /// Subscribes to slot-lifecycle updates - one notification per stage a slot moves through
    /// (shreds received, bank created, frozen, optimistically confirmed, rooted, or dead), richer and
    /// more frequent than <see cref="SubscribeSlotsAsync"/>. Ending the enumeration sends the matching
    /// unsubscribe. This subscription is marked unstable by Solana.
    /// See <see href="https://solana.com/docs/rpc/websocket/slotsupdatessubscribe">slotsUpdatesSubscribe</see>.
    /// </summary>
    /// <param name="cancellationToken">Stops the subscription when cancelled.</param>
    /// <returns>An async stream of slot-lifecycle updates.</returns>
    /// <exception cref="InvalidOperationException">Surfaced during enumeration if the connection closes or the node rejects the subscription.</exception>
    public IAsyncEnumerable<SlotsUpdate> SubscribeSlotsUpdatesAsync(CancellationToken cancellationToken = default)
        => SubscribeAsync<SlotsUpdate>("slotsUpdatesSubscribe", [], "slotsUpdatesUnsubscribe", cancellationToken);

    /// <summary>
    /// Subscribes to transaction logs mentioning <paramref name="program"/>, delivered through a channel.
    /// Cancelling <paramref name="cancellationToken"/> unsubscribes and completes the channel.
    /// See <see href="https://solana.com/docs/rpc/websocket/logssubscribe">logsSubscribe</see>.
    /// </summary>
    /// <param name="program">The program whose mentions to filter logs by.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of log notifications, each carrying its slot context and value.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public async Task<ChannelReader<RpcContextValue<LogInfo>>> SubscribeLogsAsync(
        PublicKey program,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<LogInfo>>();
        object[] parameters = [new LogsFilter { Mentions = [program] }, new CommitmentConfig { Commitment = commitment }];
        await RegisterAsync("logsSubscribe", parameters, "logsUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to logs using the full upstream <c>all</c>, <c>allWithVotes</c>, or single-address
    /// <c>mentions</c> filter union.
    /// </summary>
    /// <param name="filter">The log subscription filter.</param>
    /// <param name="commitment">The commitment level at which logs are delivered.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of context-wrapped log notifications.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public async Task<ChannelReader<RpcContextValue<LogInfo>>> SubscribeLogsWithFilterAsync(
        LogsSubscriptionFilter filter,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        object filterPayload = filter.Kind switch
        {
            LogsSubscriptionFilterKind.All => "all",
            LogsSubscriptionFilterKind.AllWithVotes => "allWithVotes",
            LogsSubscriptionFilterKind.Mentions => new LogsFilter { Mentions = [filter.Mention!.Value] },
            _ => throw new ArgumentOutOfRangeException(nameof(filter), "Unknown log subscription filter kind.")
        };

        var sink = CreateSubscriptionSink<RpcContextValue<LogInfo>>();
        object[] parameters = [filterPayload, new CommitmentConfig { Commitment = commitment }];
        await RegisterAsync("logsSubscribe", parameters, "logsUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to changes to <paramref name="account"/>, delivered through a channel. Account data is
    /// requested as base64 and exposed decoded on <see cref="AccountInfo.Data"/>. Cancelling
    /// <paramref name="cancellationToken"/> unsubscribes and completes the channel.
    /// See <see href="https://solana.com/docs/rpc/websocket/accountsubscribe">accountSubscribe</see>.
    /// </summary>
    /// <param name="account">The account to watch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of account notifications, each carrying its slot context and the account state.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public async Task<ChannelReader<RpcContextValue<AccountInfo>>> SubscribeAccountAsync(
        PublicKey account,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<AccountInfo>>();
        object[] parameters = [account, new AccountInfoConfig { Encoding = "base64", Commitment = commitment }];
        await RegisterAsync("accountSubscribe", parameters, "accountUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to an account with the complete set of configuration fields that pinned Agave actually
    /// applies. The returned account-data union preserves binary, base58, base64, jsonParsed (including its
    /// binary fallback), and base64+zstd responses without guessing a branch.
    /// </summary>
    /// <param name="account">The account to watch.</param>
    /// <param name="options">The effective account-subscription configuration.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of context-wrapped accounts with exact upstream data branches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public async Task<ChannelReader<RpcContextValue<RpcAccountInfo>>> SubscribeAccountWithOptionsAsync(
        PublicKey account,
        AccountSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sink = CreateSubscriptionSink<RpcContextValue<RpcAccountInfo>>();
        object[] parameters =
        [
            account,
            new AccountInfoConfig
            {
                Encoding = options.Encoding is { } encoding ? encoding.WireName : null,
                Commitment = options.Commitment
            }
        ];
        await RegisterAsync("accountSubscribe", parameters, "accountUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to changes to <paramref name="account"/>, decoded with <c>jsonParsed</c> encoding, delivered
    /// through a channel. Cancelling <paramref name="cancellationToken"/> unsubscribes and completes the channel.
    /// See <see href="https://solana.com/docs/rpc/websocket/accountsubscribe">accountSubscribe</see>.
    /// </summary>
    /// <param name="account">The account to watch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of parsed account notifications, each carrying its slot context and the decoded account.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public async Task<ChannelReader<RpcContextValue<ParsedAccountInfo>>> SubscribeParsedAccountAsync(
        PublicKey account,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<ParsedAccountInfo>>();
        object[] parameters = [account, new AccountInfoConfig { Encoding = "jsonParsed", Commitment = commitment }];
        await RegisterAsync("accountSubscribe", parameters, "accountUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to changes to every account owned by <paramref name="program"/>, optionally narrowed by
    /// filters, delivered through a channel. Account data is requested as base64 and exposed decoded on
    /// <see cref="AccountInfo.Data"/>. Cancelling <paramref name="cancellationToken"/> unsubscribes and
    /// completes the channel. See
    /// <see href="https://solana.com/docs/rpc/websocket/programsubscribe">programSubscribe</see>.
    /// </summary>
    /// <param name="program">The owning program to watch.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="filters">Filters every delivered account must satisfy (memcmp / data size); none are applied when null.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of program-account notifications, each carrying its slot context, address, and account state.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public async Task<ChannelReader<RpcContextValue<ProgramAccount>>> SubscribeProgramAsync(
        PublicKey program,
        Commitment commitment = Commitment.Confirmed,
        IReadOnlyList<AccountFilter>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<ProgramAccount>>();
        object[] parameters =
        [
            program,
            new ProgramAccountsConfig
            {
                Encoding = "base64",
                Commitment = commitment,
                Filters = filters?.Select(static filter => filter.Payload).ToArray()
            }
        ];
        await RegisterAsync("programSubscribe", parameters, "programUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to a program with the complete set of configuration fields that pinned Agave actually
    /// applies. The returned account-data union preserves every supported encoding branch exactly.
    /// </summary>
    /// <param name="program">The owning program to watch.</param>
    /// <param name="options">The effective program-subscription configuration.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of context-wrapped program accounts with exact upstream data branches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public async Task<ChannelReader<RpcContextValue<RpcProgramAccount>>> SubscribeProgramWithOptionsAsync(
        PublicKey program,
        ProgramSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var sink = CreateSubscriptionSink<RpcContextValue<RpcProgramAccount>>();
        object[] parameters =
        [
            program,
            new ProgramAccountsConfig
            {
                Encoding = options.Encoding is { } encoding ? encoding.WireName : null,
                Commitment = options.Commitment,
                Filters = options.Filters?.Select(static filter => filter.Payload).ToArray()
            }
        ];
        await RegisterAsync("programSubscribe", parameters, "programUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>Subscribes to program-account changes decoded by the node with <c>jsonParsed</c> encoding.</summary>
    /// <param name="program">The owning program to watch.</param>
    /// <param name="commitment">The commitment level at which changes are delivered.</param>
    /// <param name="filters">Filters every delivered account must satisfy; none when <c>null</c>.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of context-wrapped parsed program accounts.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public async Task<ChannelReader<RpcContextValue<ParsedProgramAccount>>> SubscribeParsedProgramAsync(
        PublicKey program,
        Commitment commitment = Commitment.Confirmed,
        IReadOnlyList<AccountFilter>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<ParsedProgramAccount>>();
        object[] parameters =
        [
            program,
            new ProgramAccountsConfig
            {
                Encoding = "jsonParsed",
                Commitment = commitment,
                Filters = filters?.Select(static filter => filter.Payload).ToArray()
            }
        ];
        await RegisterAsync("programSubscribe", parameters, "programUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to every new block, delivered through a channel. The node must be started with block
    /// subscriptions enabled (<c>--rpc-pubsub-enable-block-subscription</c>); many providers disable them.
    /// Cancelling <paramref name="cancellationToken"/> unsubscribes and completes the channel. See
    /// <see href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeBlocksCoreAsync("all", commitment, 0, cancellationToken);

    /// <summary>
    /// Subscribes to every new signatures-only block while explicitly opting into a newer numeric transaction
    /// version. The node must have block subscriptions enabled. Cancelling
    /// <paramref name="cancellationToken"/> unsubscribes and completes the channel.
    /// </summary>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksWithMaxVersionAsync(
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeBlocksCoreAsync("all", commitment, maxSupportedTransactionVersion, cancellationToken);

    /// <summary>
    /// Subscribes to new blocks that mention <paramref name="mentionsAccountOrProgram"/>, delivered through a
    /// channel. The node must be started with block subscriptions enabled
    /// (<c>--rpc-pubsub-enable-block-subscription</c>). Cancelling <paramref name="cancellationToken"/>
    /// unsubscribes and completes the channel. See
    /// <see href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</see>.
    /// </summary>
    /// <param name="mentionsAccountOrProgram">The account or program a block must mention to be delivered.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksAsync(
        PublicKey mentionsAccountOrProgram,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeBlocksCoreAsync(
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram }, commitment, 0, cancellationToken);

    /// <summary>
    /// Subscribes to signatures-only blocks that mention an account or program while explicitly opting into a
    /// newer numeric transaction version. Cancelling <paramref name="cancellationToken"/> unsubscribes and
    /// completes the channel.
    /// </summary>
    /// <param name="mentionsAccountOrProgram">The account or program a block must mention to be delivered.</param>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksWithMaxVersionAsync(
        PublicKey mentionsAccountOrProgram,
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeBlocksCoreAsync(
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram },
            commitment,
            maxSupportedTransactionVersion,
            cancellationToken);

    /// <summary>
    /// Subscribes to blocks using the exact upstream filter, encoding, transaction-details, rewards,
    /// commitment, and transaction-version configuration. The block body remains JSON because those choices
    /// change its schema.
    /// </summary>
    /// <param name="filter">All blocks or blocks mentioning one account or program.</param>
    /// <param name="options">The exact upstream block subscription configuration.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of context-wrapped configurable block updates.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="filter"/> or <paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public async Task<ChannelReader<RpcContextValue<RawBlockNotification>>> SubscribeBlocksWithOptionsAsync(
        BlockSubscriptionFilter filter,
        BlockSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(options);

        object filterPayload = filter.Mention is { } mention
            ? new BlockSubscribeFilter { MentionsAccountOrProgram = mention }
            : "all";
        var sink = CreateSubscriptionSink<RpcContextValue<RawBlockNotification>>();
        object[] parameters =
        [
            filterPayload,
            new BlockSubscribeConfig
            {
                Commitment = options.Commitment,
                Encoding = options.Encoding is { } encoding
                    ? encoding.WireName
                    : null,
                TransactionDetails = options.TransactionDetails is { } details
                    ? details.WireName
                    : null,
                ShowRewards = options.ShowRewards,
                MaxSupportedTransactionVersion = options.MaxSupportedTransactionVersion
            }
        ];
        await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    /// <summary>
    /// Subscribes to every new block with its transactions decoded into <c>jsonParsed</c> form, delivered
    /// through a channel. The node must be started with block subscriptions enabled
    /// (<c>--rpc-pubsub-enable-block-subscription</c>); many providers disable them. Cancelling
    /// <paramref name="cancellationToken"/> unsubscribes and completes the channel. See
    /// <see href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</see>.
    /// </summary>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of parsed-block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksAsync(
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeParsedBlocksCoreAsync("all", commitment, 0, cancellationToken);

    /// <summary>
    /// Subscribes to every new node-decoded block while explicitly opting into a newer numeric transaction
    /// version. V1 messages expose their execution settings on
    /// <see cref="ParsedMessage.TransactionConfig"/>. Cancelling <paramref name="cancellationToken"/>
    /// unsubscribes and completes the channel.
    /// </summary>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of parsed-block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksWithMaxVersionAsync(
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeParsedBlocksCoreAsync("all", commitment, maxSupportedTransactionVersion, cancellationToken);

    /// <summary>
    /// Subscribes to new blocks that mention <paramref name="mentionsAccountOrProgram"/>, with their
    /// transactions decoded into <c>jsonParsed</c> form, delivered through a channel. The node must be started
    /// with block subscriptions enabled (<c>--rpc-pubsub-enable-block-subscription</c>). Cancelling
    /// <paramref name="cancellationToken"/> unsubscribes and completes the channel. See
    /// <see href="https://solana.com/docs/rpc/websocket/blocksubscribe">blockSubscribe</see>.
    /// </summary>
    /// <param name="mentionsAccountOrProgram">The account or program a block must mention to be delivered.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of parsed-block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksAsync(
        PublicKey mentionsAccountOrProgram,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeParsedBlocksCoreAsync(
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram }, commitment, 0, cancellationToken);

    /// <summary>
    /// Subscribes to node-decoded blocks that mention an account or program while explicitly opting into a
    /// newer numeric transaction version. V1 messages expose their execution settings on
    /// <see cref="ParsedMessage.TransactionConfig"/>.
    /// </summary>
    /// <param name="mentionsAccountOrProgram">The account or program a block must mention to be delivered.</param>
    /// <param name="maxSupportedTransactionVersion">The highest numeric transaction version the caller accepts.</param>
    /// <param name="commitment">The commitment level to query at.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader of parsed-block notifications, each carrying its slot context and the produced block.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksWithMaxVersionAsync(
        PublicKey mentionsAccountOrProgram,
        byte maxSupportedTransactionVersion,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => SubscribeParsedBlocksCoreAsync(
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram },
            commitment,
            maxSupportedTransactionVersion,
            cancellationToken);

    /// <summary>
    /// Subscribes to a single notification fired when <paramref name="signature"/> reaches
    /// <paramref name="commitment"/>; the node unsubscribes automatically afterward. Prefer
    /// <see cref="ConfirmSignatureAsync"/> for the common "await one confirmation" case. See
    /// <see href="https://solana.com/docs/rpc/websocket/signaturesubscribe">signatureSubscribe</see>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to watch.</param>
    /// <param name="commitment">The commitment level to wait for.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader that yields the single signature notification.</returns>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before the subscription was confirmed.</exception>
    public async Task<ChannelReader<RpcContextValue<SignatureNotification>>> SubscribeSignatureAsync(
        string signature,
        Commitment commitment = Commitment.Confirmed,
        CancellationToken cancellationToken = default)
        => await SubscribeSignatureCoreAsync(
            signature,
            commitment,
            enableReceivedNotification: null,
            cancellationToken);

    /// <summary>
    /// Subscribes to a signature with optional early receipt notification. When enabled, the channel first
    /// receives <see cref="SignatureNotificationKind.Received"/> and remains active until the final
    /// <see cref="SignatureNotificationKind.Processed"/> result.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to watch.</param>
    /// <param name="options">Commitment and early-notification options.</param>
    /// <param name="cancellationToken">Unsubscribes and completes the channel when cancelled.</param>
    /// <returns>A channel reader that yields the received event when requested and then the final result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The node rejected the subscription, or the connection closed.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled before acknowledgement.</exception>
    public Task<ChannelReader<RpcContextValue<SignatureNotification>>> SubscribeSignatureWithOptionsAsync(
        string signature,
        SignatureSubscriptionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        return SubscribeSignatureCoreAsync(
            signature,
            options.Commitment,
            options.EnableReceivedNotification,
            cancellationToken);
    }

    /// <summary>
    /// Waits over the WebSocket until <paramref name="signature"/> reaches <paramref name="commitment"/> and
    /// returns its result - a push-based alternative to polling <c>getSignatureStatuses</c>. A confirmed-but-failed
    /// transaction is returned, not thrown; inspect <see cref="SignatureNotification.IsError"/>.
    /// </summary>
    /// <param name="signature">The transaction signature (base58) to confirm.</param>
    /// <param name="commitment">The commitment level to wait for.</param>
    /// <param name="timeout">How long to wait before giving up; defaults to 60 seconds.</param>
    /// <param name="cancellationToken">A token to cancel the wait.</param>
    /// <returns>The signature's result once it reaches <paramref name="commitment"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is negative and not infinite.</exception>
    /// <exception cref="TimeoutException">The signature was not confirmed in time.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SignatureNotification> ConfirmSignatureAsync(
        string signature,
        Commitment commitment = Commitment.Confirmed,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);

        var confirmationTimeout = timeout ?? TimeSpan.FromSeconds(60);
        if (confirmationTimeout < TimeSpan.Zero && confirmationTimeout != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The confirmation timeout must be non-negative or infinite.");

        using var timeoutCts = new CancellationTokenSource();
        var timeoutTask = confirmationTimeout == Timeout.InfiniteTimeSpan
            ? Task.CompletedTask
            : CancelAfterAsync(timeoutCts, confirmationTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var reader = await SubscribeSignatureAsync(signature, commitment, linkedCts.Token);
            var notification = await reader.ReadAsync(linkedCts.Token);
            return notification.Value!;
        }
        catch (OperationCanceledException exception)
            when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Signature {signature} was not confirmed at {commitment} within the timeout.", exception);
        }
        finally
        {
            await timeoutCts.CancelAsync();
            await timeoutTask;
        }
    }

    /// <summary>
    /// Closes the connection and ends every subscription: active channels and streams complete without an
    /// error, and a subscribe still awaiting its acknowledgement faults with
    /// <see cref="ObjectDisposedException"/>. Safe to call more than once.
    /// </summary>
    /// <returns>A task that completes once cleanup is done.</returns>
    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        ConnectionEpoch? connection;
        ConnectionEpoch? connecting;
        Task? runLoop;
        Task? connectTask;
        Task sendOperationsDrained;

        lock (_stateGate)
        {
            if (_disposeTask is not null)
                return new(_disposeTask);

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            _phase = ClientPhase.Disposing;
            runLoop = _runLoop;
            connectTask = _connectTask;
            connection = _connection;
            connecting = _connecting;
            _connection = null;
            _connecting = null;
            sendOperationsDrained = _sendOperationCount == 0
                ? Task.CompletedTask
                : _sendOperationsDrained!.Task;
        }

        _ = DisposeCoreAsync(
            completion, connection, connecting, connectTask, runLoop, sendOperationsDrained);
        return new(completion.Task);
    }

    private static async Task CancelAfterAsync(CancellationTokenSource source, TimeSpan timeout)
    {
        try
        {
            while (timeout > MaximumTimerDuration)
            {
                await Task.Delay(MaximumTimerDuration, source.Token);
                timeout -= MaximumTimerDuration;
            }

            await Task.Delay(timeout, source.Token);
            await source.CancelAsync();
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            // Confirmation finished or caller cancellation won; the timeout task only needs to stop.
        }
    }

    private static async Task SuppressAsync(Task task)
    {
        try
        {
            await task;
        }
        catch
        {
            // The owning operation has already translated or logged the failure.
        }
    }

    private static ConnectionEpochEndedException ConnectionChangedBeforeSend()
        => new(new InvalidOperationException("The WebSocket connection changed before the request was sent."));

    private static string CreateCoalescingKey(string method, object[] parameters)
    {
        var normalized = (method, parameters) switch
        {
            ("accountSubscribe", [var account, AccountInfoConfig config]) =>
            [
                account,
                new AccountInfoConfig
                {
                    Encoding = config.Encoding ?? "binary",
                    Commitment = config.Commitment ?? Commitment.Finalized,
                    DataSlice = config.DataSlice
                }
            ],
            ("programSubscribe", [var program, ProgramAccountsConfig config]) =>
            [
                program,
                new ProgramAccountsConfig
                {
                    Encoding = config.Encoding ?? "binary",
                    Commitment = config.Commitment ?? Commitment.Finalized,
                    DataSlice = config.DataSlice,
                    Filters = NormalizeProgramFilters(config.Filters),
                    WithContext = config.WithContext ?? false
                }
            ],
            ("logsSubscribe", [var filter, CommitmentConfig config]) =>
            [filter, new CommitmentConfig { Commitment = config.Commitment ?? Commitment.Finalized }],
            ("signatureSubscribe", [var signature, SignatureSubscribeConfig config]) =>
            [
                signature,
                new SignatureSubscribeConfig
                {
                    Commitment = config.Commitment ?? Commitment.Finalized,
                    EnableReceivedNotification = config.EnableReceivedNotification ?? false
                }
            ],
            ("blockSubscribe", [var filter, BlockSubscribeConfig config]) =>
            [
                filter,
                new BlockSubscribeConfig
                {
                    Commitment = config.Commitment ?? Commitment.Finalized,
                    Encoding = config.Encoding ?? "base64",
                    TransactionDetails = config.TransactionDetails ?? "full",
                    ShowRewards = config.ShowRewards ?? false,
                    MaxSupportedTransactionVersion = config.MaxSupportedTransactionVersion
                }
            ],
            _ => parameters
        };

        return JsonSerializer.Serialize(
            new RpcRequest { Id = 0, Method = method, Params = normalized },
            RpcJson.TypeInfo<RpcRequest>());
    }

    private static object NormalizeProgramFilter(object filter)
    {
        if (filter is not MemcmpFilter { Memcmp: var memcmp })
            return filter;

        var bytes = memcmp.Encoding switch
        {
            "base58" => Base58.Decode(memcmp.Bytes),
            "base64" => Convert.FromBase64String(memcmp.Bytes),
            _ => null
        };
        return bytes is null
            ? filter
            : new RawMemcmpFilter
            {
                Memcmp = new() { Offset = memcmp.Offset, Bytes = bytes, Encoding = "bytes" }
            };
    }

    private static object[] NormalizeProgramFilters(object[]? filters)
        => filters?.Select(NormalizeProgramFilter).ToArray() ?? [];

    private static bool RemovePendingSubscriptionLocked(PendingSubscribe pending, Subscription subscription)
    {
        pending.Subscriptions.Remove(subscription);
        if (ReferenceEquals(subscription.Attempt, pending))
            subscription.Attempt = null;
        return pending.Subscriptions.Count == 0;
    }

    private async Task ConnectInitialAsync(
        Uri endpoint,
        TaskCompletionSource completion,
        CancellationToken cancellationToken)
    {
        ConnectionEpoch? epoch = null;
        try
        {
            epoch = CreateConnectionEpoch();
            lock (_stateGate)
            {
                ObjectDisposedException.ThrowIf(_phase != ClientPhase.Connecting, this);

                _connecting = epoch;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCts.Token, epoch.Token);
            await epoch.Connection.ConnectAsync(endpoint, linked.Token);

            lock (_stateGate)
            {
                ObjectDisposedException.ThrowIf(
                    _phase != ClientPhase.Connecting || !ReferenceEquals(_connecting, epoch),
                    this);

                _connecting = null;
                _connection = epoch;
                _phase = ClientPhase.Connected;
                _runLoop = Task.Run(() => RunAsync(epoch, _lifetimeCts.Token), CancellationToken.None);
            }

            completion.TrySetResult();
        }
        catch (Exception exception)
        {
            lock (_stateGate)
            {
                if (ReferenceEquals(_connecting, epoch))
                    _connecting = null;
                if (_phase == ClientPhase.Connecting)
                    _phase = ClientPhase.New;
            }

            if (epoch is not null)
                await epoch.DisposeOnceAsync();

            if (exception is OperationCanceledException canceled)
            {
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else if (_lifetimeCts.IsCancellationRequested)
                    completion.TrySetException(new ObjectDisposedException(nameof(SolanaWsClient), canceled.Message));
                else
                    completion.TrySetCanceled(canceled.CancellationToken);
            }
            else
                completion.TrySetException(exception);
        }
    }

    private async Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksCoreAsync(
        object filter,
        Commitment commitment,
        byte maxSupportedTransactionVersion,
        CancellationToken cancellationToken)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<BlockNotification>>();
        object[] parameters =
        [
            filter,
            new BlockSubscribeConfig
            {
                Commitment = commitment,
                Encoding = "json",
                TransactionDetails = "signatures",
                ShowRewards = false,
                MaxSupportedTransactionVersion = maxSupportedTransactionVersion
            }
        ];
        await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    private async Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksCoreAsync(
        object filter,
        Commitment commitment,
        byte maxSupportedTransactionVersion,
        CancellationToken cancellationToken)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<ParsedBlockNotification>>();
        object[] parameters =
        [
            filter,
            new BlockSubscribeConfig
            {
                Commitment = commitment,
                Encoding = "jsonParsed",
                TransactionDetails = "full",
                ShowRewards = false,
                MaxSupportedTransactionVersion = maxSupportedTransactionVersion
            }
        ];
        await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

    private async Task<ChannelReader<RpcContextValue<SignatureNotification>>> SubscribeSignatureCoreAsync(
        string signature,
        Commitment? commitment,
        bool? enableReceivedNotification,
        CancellationToken cancellationToken)
    {
        var sink = CreateSubscriptionSink<RpcContextValue<SignatureNotification>>();
        object[] parameters =
        [
            signature,
            new SignatureSubscribeConfig
            {
                Commitment = commitment,
                EnableReceivedNotification = enableReceivedNotification
            }
        ];
        await RegisterAsync(
            "signatureSubscribe",
            parameters,
            "signatureUnsubscribe",
            sink,
            cancellationToken,
            OneShotBehavior.SignatureFinal,
            allowReceivedNotification: enableReceivedNotification is true);
        return sink.Reader;
    }

    private SubscriptionSink<T> CreateSubscriptionSink<T>()
        => new(
            _options.SubscriptionBufferCapacity,
            _options.MaxBufferedNotificationBytesPerSubscription,
            _notificationBufferBudget);

    private async IAsyncEnumerable<T> SubscribeAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sink = CreateSubscriptionSink<T>();
        var subscription = await RegisterAsync(subscribeMethod, subscribeParams, unsubscribeMethod, sink, cancellationToken);

        try
        {
            await foreach (var item in sink.Reader.ReadAllAsync(cancellationToken))
                yield return item;
        }
        finally
        {
            try
            {
                var work = TryTerminate(subscription, exception: null, unsubscribe: true);
                if (work is not null)
                    await ExecuteTerminalWorkAsync(work);
            }
            finally
            {
                // No reader escapes this iterator. If enumeration stops early, discard anything that
                // raced into its channel so the shared byte budget is returned deterministically.
                sink.DiscardBuffered();
            }
        }
    }

    private async Task<Subscription> RegisterAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        SubscriptionSink<T> sink,
        CancellationToken cancellationToken,
        OneShotBehavior oneShotBehavior = OneShotBehavior.None,
        bool allowReceivedNotification = false)
    {
        Subscription subscription;
        ConnectionEpoch epoch;
        lock (_stateGate)
        {
            ObjectDisposedException.ThrowIf(_phase is ClientPhase.Disposing or ClientPhase.Disposed, this);
            if (_phase != ClientPhase.Connected || _connection is null)
                throw new InvalidOperationException("The client is not connected.");

            var localId = ++_nextLocalId;
            subscription = new(
                localId,
                subscribeMethod,
                subscribeParams,
                unsubscribeMethod,
                sink,
                oneShotBehavior,
                allowReceivedNotification);
            _active.Add(localId, subscription);
            epoch = _connection;
        }

        await AttachCancellationAsync(subscription, cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EstablishAsync(subscription, epoch, cancellationToken);
        }
        catch (Exception exception)
        {
            var work = TryTerminate(subscription, exception, unsubscribe: true);
            if (work is not null)
                await ExecuteTerminalWorkAsync(work);

            throw;
        }

        return subscription;
    }

    // Sends the subscribe request and waits for the server to assign a subscription id. The receive
    // loop must be running concurrently to route the acknowledgement, so this is never awaited from it.
    private async Task EstablishAsync(
        Subscription subscription,
        ConnectionEpoch epoch,
        CancellationToken cancellationToken)
    {
        PendingSubscribe pending;
        Task<ulong> acknowledgement;
        var ownsRequest = false;
        lock (_stateGate)
        {
            if (subscription.Phase == SubscriptionPhase.Terminal)
                throw new OperationCanceledException(cancellationToken);
            if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
                throw ConnectionChangedBeforeSend();

            if (TryAttachToExistingRouteLocked(subscription, epoch))
                return;

            pending = _pending.Values.FirstOrDefault(candidate =>
                candidate.State == PendingState.Awaiting &&
                ReferenceEquals(candidate.Epoch, epoch) &&
                string.Equals(candidate.CoalescingKey, subscription.CoalescingKey, StringComparison.Ordinal))!;
            if (pending is not null)
            {
                pending.Add(subscription);
            }
            else
            {
                if (_pending.Count >= _options.MaxPendingSubscriptionRequests)
                {
                    throw new InvalidOperationException(
                        $"The maximum of {_options.MaxPendingSubscriptionRequests} pending subscription requests has been reached.");
                }

                var requestId = ++_nextRequestId;
                pending = new(requestId, epoch, subscription);
                subscription.Attempt = pending;
                _pending.Add(requestId, pending);
                ownsRequest = true;
            }

            acknowledgement = pending.Acked.Task;
        }

        try
        {
            if (ownsRequest)
            {
                var sendTask = SendAsync(
                    epoch,
                    new()
                    {
                        Id = pending.RequestId,
                        Method = subscription.SubscribeMethod,
                        Params = subscription.Params
                    },
                    CancellationToken.None,
                    pending: pending);

                try
                {
                    // The physical request is shared by every coalesced local subscriber. Caller
                    // cancellation stops only this waiter; the pending group owns the transport send.
                    // A compatible late ACK may also make this send redundant while it is blocked.
                    var completed = await Task.WhenAny(sendTask, acknowledgement).WaitAsync(cancellationToken);
                    if (ReferenceEquals(completed, acknowledgement))
                    {
                        _ = ObservePendingSendAsync(sendTask, pending);
                        await acknowledgement;
                        return;
                    }

                    await sendTask;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _ = ObservePendingSendAsync(sendTask, pending);
                    throw;
                }
                catch (Exception exception)
                {
                    FailPending(pending, exception);
                    if (acknowledgement.IsCompletedSuccessfully)
                        return;
                    throw;
                }
            }

            await acknowledgement.WaitAsync(_options.SubscriptionAckTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            if (AbandonPendingOrObserveAcknowledged(pending, subscription, acknowledgement))
                return;

            throw new TimeoutException(
                $"The node did not acknowledge '{subscription.SubscribeMethod}' within {_options.SubscriptionAckTimeout}.",
                exception);
        }
        catch
        {
            if (AbandonPendingOrObserveAcknowledged(pending, subscription, acknowledgement))
                return;
            throw;
        }
    }

    private async Task ObservePendingSendAsync(Task sendTask, PendingSubscribe pending)
    {
        try
        {
            await sendTask;
        }
        catch (Exception exception)
        {
            FailPending(pending, exception);
        }
    }

    private bool TryAttachToExistingRouteLocked(Subscription subscription, ConnectionEpoch epoch)
    {
        foreach (var (key, route) in _byServerId)
        {
            if (key.Generation != epoch.Generation ||
                !string.Equals(route.CoalescingKey, subscription.CoalescingKey, StringComparison.Ordinal) ||
                !route.CanRoute(subscription))
            {
                continue;
            }

            subscription.Binding = new(epoch, key.ServerId);
            subscription.Phase = SubscriptionPhase.Active;
            route.Subscriptions.Add(subscription);
            route.State = RouteState.Active;
            return true;
        }

        return false;
    }

    private void FailPending(PendingSubscribe pending, Exception exception)
    {
        lock (_stateGate)
        {
            if (pending.State != PendingState.Awaiting)
                return;

            _pending.Remove(pending.RequestId);
            pending.State = PendingState.Failed;
            foreach (var subscription in pending.Subscriptions)
            {
                if (ReferenceEquals(subscription.Attempt, pending))
                    subscription.Attempt = null;
            }

            pending.ClearSubscriptions();
            pending.Acked.TrySetException(exception);
        }
    }

    private bool AbandonPendingOrObserveAcknowledged(
        PendingSubscribe pending,
        Subscription subscription,
        Task<ulong> acknowledgement)
    {
        lock (_stateGate)
        {
            // A successful completion is the commit point. A late ACK may already have been routed
            // for cleanup, but it must not turn the caller's cancellation or timeout into success.
            if (acknowledgement.IsCompletedSuccessfully)
                return true;

            if (pending.State == PendingState.Awaiting && ReferenceEquals(subscription.Attempt, pending))
            {
                if (RemovePendingSubscriptionLocked(pending, subscription))
                {
                    AbandonPendingLocked(pending);
                    pending.Acked.TrySetCanceled();
                }
            }

            return false;
        }
    }

    private void AbandonPendingLocked(PendingSubscribe pending)
    {
        if (pending.MayHaveBeenSent)
        {
            // A possibly-sent request needs a generation-scoped tombstone so a late successful ACK
            // can be unsubscribed. Requests cancelled before the physical send need no such entry.
            pending.State = PendingState.Abandoned;
        }
        else
        {
            _pending.Remove(pending.RequestId);
            pending.State = PendingState.Failed;
        }
    }

    private async ValueTask AttachCancellationAsync(
        Subscription subscription,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return;

        var state = new CancellationState(this, subscription, cancellationToken);
        var registration = cancellationToken.UnsafeRegister(
            static callbackState => ((CancellationState)callbackState!).Cancel(), state);

        var keep = false;
        lock (_stateGate)
        {
            if (subscription.Phase != SubscriptionPhase.Terminal)
            {
                subscription.CancellationRegistration = registration;
                subscription.HasCancellationRegistration = true;
                _cancellationRegistrationCount++;
                keep = true;
            }
        }

        // Register invokes synchronously for an already-cancelled token. The callback may therefore
        // terminalize the subscription before this registration can be attached.
        if (!keep)
            await registration.DisposeAsync();
    }

    private void Cancel(Subscription subscription, CancellationToken cancellationToken)
    {
        var work = TryTerminate(
            subscription, new OperationCanceledException(cancellationToken), unsubscribe: true);
        if (work is null)
            return;

        work.Subscription.Sink.Complete(work.Exception);
        work.CancellationRegistration?.Dispose();

        if (work.Binding is not null)
            _ = SendUnsubscribeReservedAsync(
                work.Binding.Value,
                work.Subscription.UnsubscribeMethod,
                work.SendReservationHeld,
                work.ClosingRoute);
    }

    private TerminalWork? TryTerminate(
        Subscription subscription,
        Exception? exception,
        bool unsubscribe)
    {
        lock (_stateGate)
            return TryTerminateLocked(subscription, exception, unsubscribe);
    }

    private TerminalWork? TryTerminateLocked(
        Subscription subscription,
        Exception? exception,
        bool unsubscribe)
    {
        if (subscription.Phase == SubscriptionPhase.Terminal)
            return null;

        subscription.Phase = SubscriptionPhase.Terminal;
        _active.Remove(subscription.LocalId);

        if (subscription.Attempt is { } attempt && attempt.State == PendingState.Awaiting)
        {
            if (RemovePendingSubscriptionLocked(attempt, subscription))
            {
                AbandonPendingLocked(attempt);
                if (exception is OperationCanceledException canceled)
                    attempt.Acked.TrySetCanceled(canceled.CancellationToken);
                else
                    attempt.Acked.TrySetException(
                        exception ?? new InvalidOperationException("The subscription ended before acknowledgement."));
            }
        }

        RouteBinding? unsubscribeBinding = null;
        RouteGroup? closingRoute = null;
        if (subscription.Binding is { } binding)
        {
            var key = (binding.Epoch.Generation, binding.ServerId);
            if (_byServerId.TryGetValue(key, out var route) &&
                route.Subscriptions.Remove(subscription) &&
                route.Subscriptions.Count == 0)
            {
                if (unsubscribe)
                {
                    route.State = RouteState.Closing;
                    unsubscribeBinding = binding;
                    closingRoute = route;
                }
                else
                {
                    _byServerId.Remove(key);
                    route.State = RouteState.Unsubscribing;
                }
            }

            subscription.Binding = null;
        }

        CancellationTokenRegistration? registration = null;
        if (subscription.HasCancellationRegistration)
        {
            registration = subscription.CancellationRegistration;
            subscription.HasCancellationRegistration = false;
            _cancellationRegistrationCount--;
        }

        var reservationHeld = unsubscribeBinding is not null && TryReserveSendLocked(unsubscribeBinding.Value.Epoch);
        return new(subscription, exception, unsubscribeBinding, closingRoute, registration, reservationHeld);
    }

    private async Task ExecuteTerminalWorkAsync(TerminalWork work)
    {
        work.Subscription.Sink.Complete(work.Exception);
        if (work.CancellationRegistration is { } registration)
            await registration.DisposeAsync();

        if (work.Binding is not null)
            await SendUnsubscribeReservedAsync(
                work.Binding.Value,
                work.Subscription.UnsubscribeMethod,
                work.SendReservationHeld,
                work.ClosingRoute);
    }

    private async Task SendUnsubscribeReservedAsync(
        RouteBinding binding,
        string method,
        bool reservationHeld,
        RouteGroup? closingRoute = null)
    {
        if (!reservationHeld)
            return;

        try
        {
            int requestId;
            lock (_stateGate)
                requestId = ++_nextRequestId;

            await SendAsync(
                binding.Epoch,
                new() { Id = requestId, Method = method, Params = [binding.ServerId] },
                _lifetimeCts.Token,
                reservationHeld: true,
                closingRoute: closingRoute);
        }
        catch (Exception exception)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    exception,
                    "Solana WS unsubscribe '{Method}' (id {SubscriptionId}) failed",
                    method,
                    binding.ServerId);
            }
        }
    }

    private async Task SendAsync(
        ConnectionEpoch epoch,
        RpcRequest request,
        CancellationToken cancellationToken,
        bool reservationHeld = false,
        PendingSubscribe? pending = null,
        RouteGroup? closingRoute = null)
    {
        if (!reservationHeld)
        {
            lock (_stateGate)
            {
                if (!TryReserveSendLocked(epoch))
                    throw ConnectionChangedBeforeSend();
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(request, RpcJson.TypeInfo<RpcRequest>());
            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCts.Token, epoch.Token);
            await _sendLock.WaitAsync(waitCancellation.Token);
            try
            {
                lock (_stateGate)
                {
                    if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
                        throw ConnectionChangedBeforeSend();

                    if (closingRoute is not null)
                    {
                        var key = (epoch.Generation, closingRoute.ServerId);
                        if (!_byServerId.TryGetValue(key, out var currentRoute) ||
                            !ReferenceEquals(currentRoute, closingRoute) ||
                            closingRoute.State != RouteState.Closing ||
                            closingRoute.Subscriptions.Count != 0)
                        {
                            return;
                        }

                        _byServerId.Remove(key);
                        closingRoute.State = RouteState.Unsubscribing;
                    }

                    if (pending is not null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (pending.State != PendingState.Awaiting ||
                            !_pending.TryGetValue(pending.RequestId, out var current) ||
                            !ReferenceEquals(current, pending))
                        {
                            throw new InvalidOperationException(
                                "The subscription ended before its request was sent.");
                        }

                        // Cancellation before this point is definitely pre-send and removes the
                        // pending entry. From here on, retain a tombstone on cancellation because
                        // the request may reach the server even if the transport later reports failure.
                        pending.MayHaveBeenSent = true;
                    }
                }

                using var transportCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                    _lifetimeCts.Token, epoch.Token);
                await epoch.Connection.SendAsync(json, transportCancellation.Token);
            }
            finally
            {
                _sendLock.Release();
            }
        }
        finally
        {
            ReleaseSendReservation();
        }
    }

    private bool TryReserveSendLocked(ConnectionEpoch epoch)
    {
        if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
            return false;

        if (_sendOperationCount++ == 0)
            _sendOperationsDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return true;
    }

    private void ReleaseSendReservation()
    {
        lock (_stateGate)
        {
            if (--_sendOperationCount == 0)
                _sendOperationsDrained!.TrySetResult();
        }
    }

    private async Task RunAsync(ConnectionEpoch epoch, CancellationToken token)
    {
        var replayTask = Task.CompletedTask;
        while (true)
        {
            Exception? failure;
            try
            {
                failure = await ReceiveUntilClosedAsync(epoch, epoch.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || epoch.Token.IsCancellationRequested)
            {
                await SuppressAsync(replayTask);
                return;
            }

            if (token.IsCancellationRequested)
            {
                await SuppressAsync(replayTask);
                return;
            }

            var reason = failure ?? new InvalidOperationException("The WebSocket connection was closed.");

            _logger.LogWarning(reason, "Solana WS connection dropped: {Reason}", reason.Message);

            EndGeneration(epoch, reason);
            await epoch.CancelAsync();
            await SuppressAsync(replayTask);
            await epoch.DisposeOnceAsync();

            var reconnected = _options.AutoReconnect
                ? await TryReconnectAsync(token)
                : null;
            if (reconnected is null)
            {
                lock (_stateGate)
                {
                    if (token.IsCancellationRequested ||
                        _phase is ClientPhase.Disposing or ClientPhase.Disposed)
                    {
                        return;
                    }
                }

                int count;
                lock (_stateGate)
                    count = _active.Count;
                _logger.LogError(
                    reason,
                    "Solana WS disconnected and not reconnected; completing {Count} subscription(s)",
                    count);
                await CompleteAllAsync(reason);
                return;
            }

            epoch = reconnected;
            int activeCount;
            lock (_stateGate)
                activeCount = _active.Count;
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Solana WS reconnected; replaying {Count} subscription(s)", activeCount);

            // Receive and replay must run concurrently so acknowledgements can be routed. The replay task
            // remains owned by this generation and is joined before another generation can be published.
            replayTask = ResubscribeAllAsync(epoch, epoch.Token);
        }
    }

    private async Task<Exception?> ReceiveUntilClosedAsync(ConnectionEpoch epoch, CancellationToken token)
    {
        try
        {
            while (true)
            {
                var received = await ReceiveWithTimeoutAsync(epoch.Connection, token);
                if (received is not { } message)
                    return null;

                await RouteAsync(message.Text, message.WireByteCount, epoch);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private async ValueTask<WebSocketTextMessage?> ReceiveWithTimeoutAsync(
        IWebSocketConnection connection,
        CancellationToken cancellationToken)
    {
        if (_options.ReceiveTimeout == Timeout.InfiniteTimeSpan)
            return await connection.ReceiveAsync(cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ReceiveTimeout);
        try
        {
            return await connection.ReceiveAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"No complete WebSocket message was received within {_options.ReceiveTimeout}.");
        }
    }

    private async Task<ConnectionEpoch?> TryReconnectAsync(CancellationToken token)
    {
        var delay = _options.ReconnectInitialDelay;
        for (var attempt = 0; _options.MaxReconnectAttempts == 0 || attempt < _options.MaxReconnectAttempts; attempt++)
        {
            ConnectionEpoch? candidate = null;
            try
            {
                await Task.Delay(delay, token);
                candidate = CreateConnectionEpoch();
                lock (_stateGate)
                {
                    ObjectDisposedException.ThrowIf(_phase != ClientPhase.Reconnecting, this);
                    _connecting = candidate;
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, candidate.Token);
                await candidate.Connection.ConnectAsync(_endpoint!, linked.Token);

                lock (_stateGate)
                {
                    ObjectDisposedException.ThrowIf(
                        _phase != ClientPhase.Reconnecting || !ReferenceEquals(_connecting, candidate),
                        this);

                    _connecting = null;
                    _connection = candidate;
                    _phase = ClientPhase.Connected;
                }

                return candidate;
            }
            catch (OperationCanceledException)
                when (token.IsCancellationRequested || candidate?.Token.IsCancellationRequested is true)
            {
                ClearConnecting(candidate);
                if (candidate is not null)
                    await candidate.DisposeOnceAsync();
                return null;
            }
            catch (Exception exception)
            {
                ClearConnecting(candidate);
                if (candidate is not null)
                    await candidate.DisposeOnceAsync();
                if (token.IsCancellationRequested)
                    return null;

                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        exception,
                        "Solana WS reconnect attempt {Attempt} failed; retrying in {Delay}",
                        attempt + 1,
                        delay);
                }

                delay = NextDelay(delay);
            }
        }

        return null;
    }

    private void ClearConnecting(ConnectionEpoch? candidate)
    {
        lock (_stateGate)
        {
            if (ReferenceEquals(_connecting, candidate))
                _connecting = null;
        }
    }

    private ConnectionEpoch CreateConnectionEpoch()
        => new(Interlocked.Increment(ref _connectionGeneration), _connectionFactory(), SafeDisposeAsync);

    private TimeSpan NextDelay(TimeSpan current)
    {
        if (current == TimeSpan.Zero)
        {
            return TimeSpan.FromTicks(Math.Min(
                TimeSpan.FromMilliseconds(1).Ticks,
                _options.ReconnectMaxDelay.Ticks));
        }

        if (current.Ticks >= _options.ReconnectMaxDelay.Ticks / 2)
            return _options.ReconnectMaxDelay;

        return TimeSpan.FromTicks(current.Ticks * 2);
    }

    private void EndGeneration(ConnectionEpoch epoch, Exception exception)
    {
        lock (_stateGate)
        {
            if (ReferenceEquals(_connection, epoch))
                _connection = null;
            if (_phase == ClientPhase.Connected)
                _phase = ClientPhase.Reconnecting;

            foreach (var key in _byServerId.Keys
                         .Where(key => key.Generation == epoch.Generation)
                         .ToArray())
                _byServerId.Remove(key);

            foreach (var subscription in _active.Values)
            {
                if (subscription.Binding is { } binding && ReferenceEquals(binding.Epoch, epoch))
                    subscription.Binding = null;
            }

            foreach (var pair in _pending
                         .Where(pair => ReferenceEquals(pair.Value.Epoch, epoch))
                         .ToArray())
            {
                _pending.Remove(pair.Key);
                var pending = pair.Value;
                if (pending.State != PendingState.Awaiting)
                    continue;

                pending.State = PendingState.Failed;
                foreach (var subscription in pending.Subscriptions)
                {
                    if (ReferenceEquals(subscription.Attempt, pending))
                        subscription.Attempt = null;
                }

                pending.ClearSubscriptions();
                pending.Acked.TrySetException(new ConnectionEpochEndedException(exception));
            }
        }
    }

    // Every replay operation is bound to this exact physical connection. A stale replay can therefore
    // neither send on nor mutate routing for a newer generation.
    private async Task ResubscribeAllAsync(ConnectionEpoch epoch, CancellationToken token)
    {
        List<Subscription> established;
        lock (_stateGate)
        {
            established =
            [
                .. _active.Values.Where(static subscription =>
                    subscription.Phase == SubscriptionPhase.Active && subscription.Binding is null)
            ];
        }

        foreach (var subscription in established)
        {
            if (token.IsCancellationRequested)
                return;

            lock (_stateGate)
            {
                if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
                    return;
                if (subscription.Phase != SubscriptionPhase.Active || subscription.Binding is not null)
                    continue;
            }

            try
            {
                await EstablishAsync(subscription, epoch, token);
            }
            catch (Exception exception)
            {
                TerminalWork? work;
                bool generationEnded;
                lock (_stateGate)
                {
                    generationEnded = exception is ConnectionEpochEndedException ||
                                      (exception is OperationCanceledException &&
                                       (token.IsCancellationRequested || _lifetimeCts.IsCancellationRequested));
                    work = generationEnded
                        ? null
                        : TryTerminateLocked(subscription, exception, unsubscribe: true);
                }

                // Only the connection epoch ending stops the replay loop. Cancellation or failure of
                // one subscription terminalizes (or has already terminalized) that subscription and
                // replay proceeds with the remaining snapshot entries.
                if (generationEnded)
                    return;

                if (work is null)
                    continue;

                _logger.LogWarning(
                    exception,
                    "Solana WS failed to replay subscription '{Method}'; faulting that subscription",
                    subscription.SubscribeMethod);
                await ExecuteTerminalWorkAsync(work);
            }
        }
    }

    private async Task RouteAsync(string message, int wireByteCount, ConnectionEpoch epoch)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (!root.TryGetProperty("jsonrpc", out var jsonRpcElement) ||
            jsonRpcElement.ValueKind != JsonValueKind.String ||
            !string.Equals(jsonRpcElement.GetString(), "2.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The node sent a WebSocket message without JSON-RPC version 2.0.");
        }

        if (root.TryGetProperty("id", out var idElement))
        {
            if (idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out var requestId))
                throw new InvalidDataException("A WebSocket JSON-RPC response carried a non-integer request id.");

            var hasResult = root.TryGetProperty("result", out var resultElement);
            var hasError = root.TryGetProperty("error", out var errorElement) &&
                           errorElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);
            if (hasResult == hasError)
            {
                throw new InvalidDataException(
                    "A WebSocket JSON-RPC response must carry exactly one of result or a non-null error.");
            }

            if (hasError)
            {
                if (errorElement.ValueKind != JsonValueKind.Object ||
                    !errorElement.TryGetProperty("code", out var codeElement) ||
                    codeElement.ValueKind != JsonValueKind.Number ||
                    !codeElement.TryGetInt32(out _) ||
                    !errorElement.TryGetProperty("message", out var messageElement) ||
                    messageElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("A WebSocket JSON-RPC response carried a malformed error object.");
                }

                CompletePendingError(requestId, epoch, errorElement);
                return;
            }

            await CompletePendingResultAsync(requestId, epoch, resultElement);
            return;
        }

        if (!root.TryGetProperty("params", out var paramsElement) ||
            !paramsElement.TryGetProperty("subscription", out var subscriptionElement) ||
            !paramsElement.TryGetProperty("result", out var notification) ||
            !subscriptionElement.TryGetUInt64(out var notified))
        {
            return;
        }

        Subscription[] subscriptions;
        List<TerminalWork>? oneShotWork = null;
        lock (_stateGate)
        {
            var key = (epoch.Generation, ServerId: notified);
            if (!_byServerId.TryGetValue(key, out var route))
                return;

            if (!root.TryGetProperty("method", out var methodElement) ||
                methodElement.ValueKind != JsonValueKind.String ||
                !string.Equals(
                    methodElement.GetString(),
                    route.NotificationMethod,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The node routed subscription id {notified} as an unexpected notification method; " +
                    $"expected '{route.NotificationMethod}'.");
            }

            if (route.State == RouteState.Closing && route.Subscriptions.Count == 0)
            {
                if (route.ShouldTerminateAfter(notification))
                {
                    _byServerId.Remove(key);
                    route.State = RouteState.Unsubscribing;
                }

                return;
            }

            if (route.State != RouteState.Active || route.Subscriptions.Count == 0)
                return;

            subscriptions = [.. route.Subscriptions];
            if (subscriptions[0].ShouldTerminateAfter(notification))
            {
                oneShotWork =
                [
                    .. subscriptions
                        .Select(subscription => TryTerminateLocked(subscription, exception: null, unsubscribe: false))
                        .OfType<TerminalWork>()
                ];
            }
        }

        if (oneShotWork is not null)
        {
            foreach (var work in oneShotWork)
            {
                var subscription = work.Subscription;
                try
                {
                    subscription.ValidateNotification(notification);
                    subscription.Sink.Deliver(notification, wireByteCount);
                    subscription.Sink.Complete(exception: null);
                }
                catch (Exception exception)
                {
                    subscription.Sink.Complete(exception);
                }

                if (work.CancellationRegistration is { } registration)
                    await registration.DisposeAsync();
            }

            return;
        }

        foreach (var subscription in subscriptions)
        {
            try
            {
                subscription.ValidateNotification(notification);
                subscription.Sink.Deliver(notification, wireByteCount);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Solana WS could not decode a '{Method}' notification; faulting that subscription",
                    subscription.SubscribeMethod);
                var work = TryTerminate(subscription, exception, unsubscribe: true);
                if (work is not null)
                    await ExecuteTerminalWorkAsync(work);
            }
        }
    }

    private async Task CompletePendingResultAsync(
        int requestId,
        ConnectionEpoch epoch,
        JsonElement result)
    {
        RouteBinding? lateBinding = null;
        string? lateUnsubscribeMethod = null;
        RouteGroup? lateClosingRoute = null;
        var reservationHeld = false;

        lock (_stateGate)
        {
            if (!_pending.TryGetValue(requestId, out var pending) ||
                !ReferenceEquals(pending.Epoch, epoch))
            {
                return;
            }

            var subscriptions = pending.Subscriptions
                .Where(subscription =>
                    subscription.Phase != SubscriptionPhase.Terminal &&
                    ReferenceEquals(subscription.Attempt, pending))
                .ToArray();
            var wasAwaiting = pending.State == PendingState.Awaiting && subscriptions.Length > 0;
            if (result.ValueKind != JsonValueKind.Number || !result.TryGetUInt64(out var subscriptionId))
            {
                _pending.Remove(requestId);
                pending.State = PendingState.Failed;
                foreach (var subscription in subscriptions)
                    subscription.Attempt = null;
                pending.ClearSubscriptions();
                if (wasAwaiting)
                    pending.Acked.TrySetException(new InvalidOperationException("The node rejected the subscription."));
                return;
            }

            var canAccept = wasAwaiting &&
                            _phase == ClientPhase.Connected &&
                            ReferenceEquals(_connection, epoch);
            var key = (epoch.Generation, subscriptionId);
            _byServerId.TryGetValue(key, out var route);
            if (canAccept && route is not null && subscriptions.Any(subscription => !route.CanRoute(subscription)))
            {
                // Leave this pending entry intact so EndGeneration can fault its waiter.
                throw new InvalidDataException(
                    $"The node assigned duplicate WebSocket subscription id {subscriptionId}.");
            }

            if (!canAccept && route is not null && !route.CanRoute(pending))
            {
                throw new InvalidDataException(
                    $"The node assigned duplicate WebSocket subscription id {subscriptionId}.");
            }

            _pending.Remove(requestId);

            if (canAccept)
            {
                pending.State = PendingState.Acknowledged;
                var binding = new RouteBinding(epoch, subscriptionId);
                if (route is null)
                {
                    route = new(subscriptions[0]) { ServerId = subscriptionId };
                    _byServerId.Add(key, route);
                }

                route.State = RouteState.Active;
                foreach (var subscription in subscriptions)
                {
                    subscription.Attempt = null;
                    subscription.Binding = binding;
                    subscription.Phase = SubscriptionPhase.Active;
                    route.Subscriptions.Add(subscription);
                }

                pending.ClearSubscriptions();
                pending.Acked.TrySetResult(subscriptionId);
            }
            else if (route is not null)
            {
                pending.State = PendingState.LateAcknowledged;
            }
            else
            {
                pending.State = PendingState.LateAcknowledged;
                var closingRoute = new RouteGroup(pending)
                {
                    ServerId = subscriptionId,
                    State = RouteState.Closing
                };
                var equivalentPending = _phase == ClientPhase.Connected && ReferenceEquals(_connection, epoch)
                    ? _pending.Values.FirstOrDefault(candidate =>
                        candidate.State == PendingState.Awaiting &&
                        ReferenceEquals(candidate.Epoch, epoch) &&
                        string.Equals(candidate.CoalescingKey, pending.CoalescingKey, StringComparison.Ordinal) &&
                        candidate.Subscriptions.Count > 0 &&
                        candidate.Subscriptions.All(closingRoute.CanRoute))
                    : null;

                if (equivalentPending is not null)
                {
                    var binding = new RouteBinding(epoch, subscriptionId);
                    _byServerId.Add(key, closingRoute);
                    closingRoute.State = RouteState.Active;
                    foreach (var subscription in equivalentPending.Subscriptions)
                    {
                        subscription.Attempt = null;
                        subscription.Binding = binding;
                        subscription.Phase = SubscriptionPhase.Active;
                        closingRoute.Subscriptions.Add(subscription);
                    }

                    equivalentPending.ClearSubscriptions();
                    if (equivalentPending.MayHaveBeenSent)
                    {
                        equivalentPending.State = PendingState.Abandoned;
                    }
                    else
                    {
                        _pending.Remove(equivalentPending.RequestId);
                        equivalentPending.State = PendingState.Acknowledged;
                    }

                    equivalentPending.Acked.TrySetResult(subscriptionId);
                }
                else
                {
                    reservationHeld = TryReserveSendLocked(epoch);
                    if (reservationHeld)
                    {
                        _byServerId.Add(key, closingRoute);
                        lateBinding = new RouteBinding(epoch, subscriptionId);
                        lateUnsubscribeMethod = pending.UnsubscribeMethod;
                        lateClosingRoute = closingRoute;
                    }
                }
            }
        }

        if (lateBinding is not null)
            await SendUnsubscribeReservedAsync(
                lateBinding.Value,
                lateUnsubscribeMethod!,
                reservationHeld,
                lateClosingRoute);
    }

    private void CompletePendingError(int requestId, ConnectionEpoch epoch, JsonElement errorElement)
    {
        var detail = errorElement.GetProperty("message").GetString()!;
        var code = errorElement.GetProperty("code").GetInt32();
        JsonElement? data = errorElement.TryGetProperty("data", out var dataElement)
            ? dataElement
            : null;

        string method;
        lock (_stateGate)
        {
            if (!_pending.TryGetValue(requestId, out var pending) ||
                !ReferenceEquals(pending.Epoch, epoch))
            {
                return;
            }

            _pending.Remove(requestId);
            method = pending.SubscribeMethod;
            var subscriptions = pending.Subscriptions.ToArray();
            var wasAwaiting = pending.State == PendingState.Awaiting && subscriptions.Length > 0;
            pending.State = PendingState.Failed;
            foreach (var subscription in subscriptions)
            {
                if (ReferenceEquals(subscription.Attempt, pending))
                    subscription.Attempt = null;
            }

            pending.ClearSubscriptions();
            if (wasAwaiting)
            {
                pending.Acked.TrySetException(
                    new InvalidOperationException(
                        $"The node rejected '{method}' (code {code}): {detail}",
                        new RpcException(code, detail, data)));
            }
        }

        _logger.LogWarning(
            "Solana WS request {RequestId} ('{Method}') rejected by the node (code {Code}): {Detail}",
            requestId,
            method,
            code,
            detail);
    }

    private async Task CompleteAllAsync(Exception? exception)
    {
        List<TerminalWork> work;
        lock (_stateGate)
        {
            if (_phase is not (ClientPhase.Disposing or ClientPhase.Disposed))
                _phase = ClientPhase.Stopped;

            var pendingException = exception ?? new ObjectDisposedException(nameof(SolanaWsClient));
            work =
            [
                .. _active.Values
                    .ToArray()
                    .Select(subscription => TryTerminateLocked(
                        subscription,
                        subscription.Phase == SubscriptionPhase.Establishing ? pendingException : exception,
                        unsubscribe: false))
                    .OfType<TerminalWork>()
            ];

            foreach (var pending in _pending.Values)
            {
                if (pending.State == PendingState.Awaiting)
                    pending.Acked.TrySetException(pendingException);
                pending.State = PendingState.Failed;
            }

            _pending.Clear();
            _byServerId.Clear();
        }

        foreach (var item in work)
            await ExecuteTerminalWorkAsync(item);
    }

    private async Task SafeDisposeAsync(IWebSocketConnection connection)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Solana WS connection dispose failed");
        }
    }

    private async Task DisposeCoreAsync(
        TaskCompletionSource completion,
        ConnectionEpoch? connection,
        ConnectionEpoch? connecting,
        Task? connectTask,
        Task? runLoop,
        Task sendOperationsDrained)
    {
        try
        {
            await _lifetimeCts.CancelAsync();

            var connectionDispose = connection?.DisposeOnceAsync() ?? Task.CompletedTask;
            var connectingDispose = connecting?.DisposeOnceAsync() ?? Task.CompletedTask;

            await CompleteAllAsync(exception: null);
            await Task.WhenAll(connectionDispose, connectingDispose);

            if (connectTask is not null)
                await SuppressAsync(connectTask);
            if (runLoop is not null)
                await SuppressAsync(runLoop);
            await sendOperationsDrained;

            _sendLock.Dispose();
            _lifetimeCts.Dispose();
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Solana WS cleanup ended with an error during dispose");
        }
        finally
        {
            lock (_stateGate)
                _phase = ClientPhase.Disposed;
            completion.TrySetResult();
        }
    }

    private readonly record struct RouteBinding(ConnectionEpoch Epoch, ulong ServerId);

    private readonly record struct BufferedNotification<T>(T Value, int EncodedMessageSizeBytes);

    private sealed class RouteGroup
    {
        private readonly string _subscribeMethod;
        private readonly string _unsubscribeMethod;
        private readonly OneShotBehavior _oneShotBehavior;

        public RouteGroup(Subscription subscription)
            : this(
                subscription.SubscribeMethod,
                subscription.NotificationMethod,
                subscription.UnsubscribeMethod,
                subscription.CoalescingKey,
                subscription.OneShotBehavior)
        {
        }

        public RouteGroup(PendingSubscribe pending)
            : this(
                pending.SubscribeMethod,
                pending.NotificationMethod,
                pending.UnsubscribeMethod,
                pending.CoalescingKey,
                pending.OneShotBehavior)
        {
        }

        private RouteGroup(
            string subscribeMethod,
            string notificationMethod,
            string unsubscribeMethod,
            string coalescingKey,
            OneShotBehavior oneShotBehavior)
        {
            _subscribeMethod = subscribeMethod;
            _unsubscribeMethod = unsubscribeMethod;
            _oneShotBehavior = oneShotBehavior;
            NotificationMethod = notificationMethod;
            CoalescingKey = coalescingKey;
        }

        public string CoalescingKey { get; }

        public string NotificationMethod { get; }

        public ulong ServerId { get; set; }

        public RouteState State { get; set; }

        public HashSet<Subscription> Subscriptions { get; } = [];

        public bool CanRoute(Subscription candidate)
            => CanRoute(
                candidate.CoalescingKey,
                candidate.SubscribeMethod,
                candidate.NotificationMethod,
                candidate.UnsubscribeMethod,
                candidate.OneShotBehavior);

        public bool CanRoute(PendingSubscribe candidate)
            => CanRoute(
                candidate.CoalescingKey,
                candidate.SubscribeMethod,
                candidate.NotificationMethod,
                candidate.UnsubscribeMethod,
                candidate.OneShotBehavior);

        public bool ShouldTerminateAfter(JsonElement notification) => _oneShotBehavior switch
        {
            OneShotBehavior.None => false,
            OneShotBehavior.SignatureFinal =>
                notification.ValueKind == JsonValueKind.Object &&
                notification.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Object,
            _ => false
        };

        private bool CanRoute(
            string coalescingKey,
            string subscribeMethod,
            string notificationMethod,
            string unsubscribeMethod,
            OneShotBehavior oneShotBehavior)
            => string.Equals(CoalescingKey, coalescingKey, StringComparison.Ordinal) &&
               string.Equals(_subscribeMethod, subscribeMethod, StringComparison.Ordinal) &&
               string.Equals(NotificationMethod, notificationMethod, StringComparison.Ordinal) &&
               string.Equals(_unsubscribeMethod, unsubscribeMethod, StringComparison.Ordinal) &&
               _oneShotBehavior == oneShotBehavior;
    }

    private sealed class PendingSubscribe(
        int requestId,
        ConnectionEpoch epoch,
        Subscription subscription)
    {
        public int RequestId { get; } = requestId;

        public ConnectionEpoch Epoch { get; } = epoch;

        public string SubscribeMethod { get; } = subscription.SubscribeMethod;

        public string UnsubscribeMethod { get; } = subscription.UnsubscribeMethod;

        public string NotificationMethod { get; } = subscription.NotificationMethod;

        public string CoalescingKey { get; } = subscription.CoalescingKey;

        public OneShotBehavior OneShotBehavior { get; } = subscription.OneShotBehavior;

        public HashSet<Subscription> Subscriptions { get; } = [subscription];

        public TaskCompletionSource<ulong> Acked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingState State { get; set; }

        public bool MayHaveBeenSent { get; set; }

        public void Add(Subscription added)
        {
            Subscriptions.Add(added);
            added.Attempt = this;
        }

        public void ClearSubscriptions() => Subscriptions.Clear();
    }

    private sealed class ConnectionEpoch
    {
        private readonly CancellationTokenSource _closed = new();
        private readonly Func<IWebSocketConnection, Task> _disposeConnection;
        private readonly Lazy<Task> _dispose;

        public ConnectionEpoch(
            long generation,
            IWebSocketConnection connection,
            Func<IWebSocketConnection, Task> disposeConnection)
        {
            Generation = generation;
            Connection = connection;
            _disposeConnection = disposeConnection;
            _dispose = new(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public long Generation { get; }

        public IWebSocketConnection Connection { get; }

        public CancellationToken Token => _closed.Token;

        public Task CancelAsync() => _closed.CancelAsync();

        public Task DisposeOnceAsync() => _dispose.Value;

        private async Task DisposeCoreAsync()
        {
            try
            {
                // Keep the epoch receive token alive while the connection performs its close
                // handshake. Client shutdown cancels the lifetime token separately, so sends and
                // connects still stop promptly while the active receive can consume the peer close.
                await _disposeConnection(Connection);
            }
            finally
            {
                await _closed.CancelAsync();
            }
        }
    }

    private sealed record TerminalWork(
        Subscription Subscription,
        Exception? Exception,
        RouteBinding? Binding,
        RouteGroup? ClosingRoute,
        CancellationTokenRegistration? CancellationRegistration,
        bool SendReservationHeld);

    private sealed record CancellationState(
        SolanaWsClient Client,
        Subscription Subscription,
        CancellationToken CancellationToken)
    {
        public void Cancel() => Client.Cancel(Subscription, CancellationToken);
    }

    private sealed class Subscription(
        long localId,
        string subscribeMethod,
        object[] parameters,
        string unsubscribeMethod,
        ISubscriptionSink sink,
        OneShotBehavior oneShotBehavior,
        bool allowReceivedNotification)
    {
        public long LocalId { get; } = localId;

        public string SubscribeMethod { get; } = subscribeMethod;

        public object[] Params { get; } = parameters;

        public string UnsubscribeMethod { get; } = unsubscribeMethod;

        public string NotificationMethod { get; } = subscribeMethod.EndsWith("Subscribe", StringComparison.Ordinal)
            ? subscribeMethod[..^"Subscribe".Length] + "Notification"
            : throw new ArgumentException("A subscription method must end with 'Subscribe'.", nameof(subscribeMethod));

        public string CoalescingKey { get; } = CreateCoalescingKey(subscribeMethod, parameters);

        public ISubscriptionSink Sink { get; } = sink;

        public OneShotBehavior OneShotBehavior { get; } = oneShotBehavior;

        public bool AllowReceivedNotification { get; } = allowReceivedNotification;

        public SubscriptionPhase Phase { get; set; } = SubscriptionPhase.Establishing;

        public PendingSubscribe? Attempt { get; set; }

        public RouteBinding? Binding { get; set; }

        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public bool HasCancellationRegistration { get; set; }

        public void ValidateNotification(JsonElement notification)
        {
            if (SubscribeMethod is "accountSubscribe" or "logsSubscribe" or "programSubscribe" or "blockSubscribe" or "signatureSubscribe")
            {
                if (notification.ValueKind != JsonValueKind.Object ||
                    !notification.TryGetProperty("value", out var value) ||
                    value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    throw new JsonException($"A '{NotificationMethod}' payload must carry a non-null value.");
                }

                if (SubscribeMethod == "signatureSubscribe" &&
                    value.ValueKind == JsonValueKind.String &&
                    !AllowReceivedNotification)
                {
                    throw new JsonException(
                        "The node sent receivedSignature although the subscription did not request early notifications.");
                }
            }
        }

        public bool ShouldTerminateAfter(JsonElement notification) => OneShotBehavior switch
        {
            OneShotBehavior.None => false,
            OneShotBehavior.SignatureFinal =>
                notification.ValueKind == JsonValueKind.Object &&
                notification.TryGetProperty("value", out var value) &&
                value.ValueKind == JsonValueKind.Object,
            _ => false
        };
    }

    private sealed class ConnectionEpochEndedException(Exception innerException)
        : InvalidOperationException(innerException.Message, innerException)
    {
    }

    /// <summary>
    /// Owns the client-wide counter and serializes it with every per-subscription counter. Accounts are
    /// deliberately not retained here: once a completed reader and its buffered values become unreachable,
    /// the account finalizer can return their aggregate reservation as a last-resort accounting backstop.
    /// </summary>
    private sealed class NotificationBufferBudget(long limit)
    {
        private readonly Lock _gate = new();
        private long _bufferedBytes;

        public long BufferedBytes
        {
            get
            {
                lock (_gate)
                    return _bufferedBytes;
            }
        }

        public long Limit => limit;

        public NotificationBufferAccount CreateAccount(long accountLimit) => new(this, accountLimit);

        public BufferReservationResult TryReserve(NotificationBufferAccount account, int byteCount)
        {
            lock (_gate)
            {
                if (account.Completed)
                    return BufferReservationResult.Completed;
                if (byteCount > account.Limit - account.BufferedBytes)
                    return BufferReservationResult.SubscriptionLimitExceeded;
                if (byteCount > limit - _bufferedBytes)
                    return BufferReservationResult.TotalLimitExceeded;

                account.BufferedBytes += byteCount;
                _bufferedBytes += byteCount;
                return BufferReservationResult.Reserved;
            }
        }

        public void Complete(NotificationBufferAccount account)
        {
            lock (_gate)
            {
                account.Completed = true;
                DisposeAccountIfDrained(account);
            }
        }

        public bool IsCompleted(NotificationBufferAccount account)
        {
            lock (_gate)
                return account.Completed;
        }

        public void Release(NotificationBufferAccount account, int byteCount)
        {
            lock (_gate)
            {
                // Releasing more than was reserved would drive the counters negative and silently *inflate*
                // the budget rather than fail, so clamp instead of trusting the caller's bookkeeping.
                var released = Math.Min(byteCount, account.BufferedBytes);
                account.BufferedBytes -= released;
                _bufferedBytes -= released;
                DisposeAccountIfDrained(account);
            }
        }

        public void ReleaseAll(NotificationBufferAccount account)
        {
            lock (_gate)
            {
                _bufferedBytes -= account.BufferedBytes;
                account.BufferedBytes = 0;
                account.Completed = true;

                // Same drain check as the other release paths: without it an account emptied this way stays
                // registered for finalization and costs a second GC cycle to collect for no benefit.
                DisposeAccountIfDrained(account);
            }
        }

        private static void DisposeAccountIfDrained(NotificationBufferAccount account)
        {
            if (account.Completed && account.BufferedBytes == 0)
                account.Dispose();
        }
    }

    private sealed class NotificationBufferAccount(NotificationBufferBudget budget, long limit) : IDisposable
    {
        private readonly NotificationBufferBudget _budget = budget;

        ~NotificationBufferAccount() => _budget.ReleaseAll(this);

        public long Limit { get; } = limit;

        public long TotalLimit => _budget.Limit;

        // Accessed only while NotificationBufferBudget._gate is held.
        public long BufferedBytes { get; set; }

        // Accessed only while NotificationBufferBudget._gate is held.
        public bool Completed { get; set; }

        public bool IsCompleted => _budget.IsCompleted(this);

        public BufferReservationResult TryReserve(int byteCount) => _budget.TryReserve(this, byteCount);

        public void Complete() => _budget.Complete(this);

        public void Release(int byteCount) => _budget.Release(this, byteCount);

        // The budget calls this only after completion and a full drain. No recovery work remains.
        public void Dispose() => GC.SuppressFinalize(this);
    }

    private sealed class BufferedNotificationReader<T>(
        ChannelReader<BufferedNotification<T>> inner,
        NotificationBufferAccount account) : ChannelReader<T>
    {
        public override Task Completion => inner.Completion;

        public override bool CanCount => inner.CanCount;

        public override int Count => inner.Count;

        public override bool CanPeek => inner.CanPeek;

        public override ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
        {
            var read = inner.ReadAsync(cancellationToken);
            if (!read.IsCompletedSuccessfully)
                return CompleteReadAsync(read, account);

            var buffered = read.Result;
            account.Release(buffered.EncodedMessageSizeBytes);
            return ValueTask.FromResult(buffered.Value);
        }

        public override bool TryPeek([MaybeNullWhen(false)] out T item)
        {
            if (inner.TryPeek(out var buffered))
            {
                item = buffered.Value;
                return true;
            }

            item = default;
            return false;
        }

        public override bool TryRead([MaybeNullWhen(false)] out T item)
        {
            if (!inner.TryRead(out var buffered))
            {
                item = default;
                return false;
            }

            item = buffered.Value;
            account.Release(buffered.EncodedMessageSizeBytes);
            return true;
        }

        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
            => inner.WaitToReadAsync(cancellationToken);

        private static async ValueTask<T> CompleteReadAsync(
            ValueTask<BufferedNotification<T>> read,
            NotificationBufferAccount bufferAccount)
        {
            var buffered = await read;
            bufferAccount.Release(buffered.EncodedMessageSizeBytes);
            return buffered.Value;
        }
    }

    private sealed class SubscriptionSink<T> : ISubscriptionSink
    {
        private readonly Channel<BufferedNotification<T>> _channel;
        private readonly int _capacity;
        private readonly NotificationBufferAccount _bufferAccount;
        private readonly BufferedNotificationReader<T> _reader;

        // Resolved once per sink so an unregistered notification type fails at subscribe time, not on
        // first delivery deep inside the receive loop.
        private readonly JsonTypeInfo<T> _typeInfo = RpcJson.TypeInfo<T>();

        public SubscriptionSink(
            int capacity,
            long maxBufferedBytes,
            NotificationBufferBudget sharedBudget)
        {
            _capacity = capacity;
            _bufferAccount = sharedBudget.CreateAccount(maxBufferedBytes);
            _channel = Channel.CreateBounded<BufferedNotification<T>>(new BoundedChannelOptions(capacity)
            {
                SingleWriter = false,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _reader = new(_channel.Reader, _bufferAccount);
        }

        public ChannelReader<T> Reader => _reader;

        public void Deliver(JsonElement result, int encodedMessageSizeBytes)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(encodedMessageSizeBytes);

            switch (_bufferAccount.TryReserve(encodedMessageSizeBytes))
            {
                case BufferReservationResult.Completed:
                    return;
                case BufferReservationResult.SubscriptionLimitExceeded:
                    throw new InvalidOperationException(
                        $"Subscription notification buffer exceeded its encoded-size limit of " +
                        $"{_bufferAccount.Limit} byte(s).");
                case BufferReservationResult.TotalLimitExceeded:
                    throw new InvalidOperationException(
                        "The client's total notification buffer exceeded its encoded-size limit of " +
                        $"{_bufferAccount.TotalLimit} byte(s).");
            }

            var written = false;
            try
            {
                var value = result.Deserialize(_typeInfo)
                            ?? throw new JsonException("A WebSocket notification result decoded to null.");

                written = _channel.Writer.TryWrite(new(value, encodedMessageSizeBytes));
                if (written || _bufferAccount.IsCompleted)
                    return;

                throw new InvalidOperationException(
                    $"Subscription notification buffer exceeded its capacity of {_capacity} item(s).");
            }
            finally
            {
                if (!written)
                    _bufferAccount.Release(encodedMessageSizeBytes);
            }
        }

        public void Complete(Exception? exception)
        {
            _bufferAccount.Complete();
            _channel.Writer.TryComplete(exception);
        }

        public void DiscardBuffered()
        {
            while (_channel.Reader.TryRead(out var buffered))
                _bufferAccount.Release(buffered.EncodedMessageSizeBytes);
        }
    }
}
