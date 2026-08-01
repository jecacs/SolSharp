using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    private readonly Func<IWebSocketConnection> _connectionFactory;
    private readonly SolanaWsClientOptions _options;
    private readonly object _stateGate = new();
    private readonly Dictionary<int, PendingSubscribe> _pending = [];
    private readonly Dictionary<long, Subscription> _active = [];
    private readonly Dictionary<(long Generation, long ServerId), Subscription> _byServerId = [];
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly ILogger _logger;

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

    internal int RetainedCancellationRegistrationCount
    {
        get
        {
            lock (_stateGate)
                return _cancellationRegistrationCount;
        }
    }

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/> with default options.</summary>
    /// <param name="loggerFactory">Optional factory for connection/reconnection diagnostics; no logging when null.</param>
    public SolanaWsClient(ILoggerFactory? loggerFactory = null) : this(new SolanaWsClientOptions(), loggerFactory)
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
        if (options.ReconnectInitialDelay < TimeSpan.Zero || options.ReconnectInitialDelay > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Initial reconnect delay must be non-negative and supported by a timer.");
        if (options.ReconnectMaxDelay < options.ReconnectInitialDelay || options.ReconnectMaxDelay > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum reconnect delay must be at least the initial delay and supported by a timer.");
        if (options.MaxReconnectAttempts < 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum reconnect attempts cannot be negative.");
        if (options.SubscriptionAckTimeout <= TimeSpan.Zero || options.SubscriptionAckTimeout > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Subscription acknowledgement timeout must be positive and finite.");
        if (options.ReceiveTimeout != Timeout.InfiniteTimeSpan && options.ReceiveTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Receive timeout must be positive or infinite.");
        if (options.ReceiveTimeout > MaximumTimerDuration)
            throw new ArgumentOutOfRangeException(nameof(options), "Receive timeout is too large for a timer.");

        _connectionFactory = connectionFactory;
        _options = options;
        _logger = loggerFactory?.CreateLogger<SolanaWsClient>() ?? NullLogger<SolanaWsClient>.Instance;
    }

    internal SolanaWsClient(IWebSocketConnection connection)
        : this(() => connection, new SolanaWsClientOptions { AutoReconnect = false })
    {
    }

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
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _connectTask = completion.Task;
        }

        _ = ConnectInitialAsync(endpoint, cancellationToken, completion);
        return completion.Task;
    }

    private async Task ConnectInitialAsync(
        Uri endpoint,
        CancellationToken cancellationToken,
        TaskCompletionSource completion)
    {
        ConnectionEpoch? epoch = null;
        try
        {
            epoch = CreateConnectionEpoch();
            lock (_stateGate)
            {
                if (_phase != ClientPhase.Connecting)
                    throw new ObjectDisposedException(nameof(SolanaWsClient));

                _connecting = epoch;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCts.Token, epoch.Token);
            await epoch.Connection.ConnectAsync(endpoint, linked.Token);

            lock (_stateGate)
            {
                if (_phase != ClientPhase.Connecting || !ReferenceEquals(_connecting, epoch))
                    throw new ObjectDisposedException(nameof(SolanaWsClient));

                _connecting = null;
                _connection = epoch;
                _phase = ClientPhase.Connected;
                _runLoop = Task.Run(() => RunAsync(epoch, _lifetimeCts.Token));
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
                Filters = filters?.Select(filter => filter.Payload).ToArray()
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
        => SubscribeBlocksCoreAsync("all", commitment, cancellationToken);

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
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram }, commitment, cancellationToken);

    private async Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksCoreAsync(
        object filter,
        Commitment commitment,
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
                MaxSupportedTransactionVersion = 0
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
        => SubscribeParsedBlocksCoreAsync("all", commitment, cancellationToken);

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
            new BlockSubscribeFilter { MentionsAccountOrProgram = mentionsAccountOrProgram }, commitment, cancellationToken);

    private async Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksCoreAsync(
        object filter,
        Commitment commitment,
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
                MaxSupportedTransactionVersion = 0
            }
        ];
        await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);
        return sink.Reader;
    }

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
    {
        var sink = CreateSubscriptionSink<RpcContextValue<SignatureNotification>>();
        object[] parameters = [signature, new CommitmentConfig { Commitment = commitment }];
        await RegisterAsync(
            "signatureSubscribe", parameters, "signatureUnsubscribe", sink, cancellationToken, oneShot: true);
        return sink.Reader;
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
    /// <exception cref="TimeoutException">The signature was not confirmed in time.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<SignatureNotification> ConfirmSignatureAsync(
        string signature,
        Commitment commitment = Commitment.Confirmed,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signature);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout ?? TimeSpan.FromSeconds(60));

        var reader = await SubscribeSignatureAsync(signature, commitment, timeoutCts.Token);
        try
        {
            var notification = await reader.ReadAsync(timeoutCts.Token);
            return notification.Value!;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Signature {signature} was not confirmed at {commitment} within the timeout.");
        }
        finally
        {
            await timeoutCts.CancelAsync();
        }
    }

    private SubscriptionSink<T> CreateSubscriptionSink<T>()
        => new(_options.SubscriptionBufferCapacity);

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
            var work = TryTerminate(subscription, exception: null, unsubscribe: true);
            if (work is not null)
                await ExecuteTerminalWorkAsync(work);
        }
    }

    private async Task<Subscription> RegisterAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        SubscriptionSink<T> sink,
        CancellationToken cancellationToken,
        bool oneShot = false)
    {
        Subscription subscription;
        ConnectionEpoch epoch;
        lock (_stateGate)
        {
            if (_phase is ClientPhase.Disposing or ClientPhase.Disposed)
                throw new ObjectDisposedException(nameof(SolanaWsClient));
            if (_phase != ClientPhase.Connected || _connection is null)
                throw new InvalidOperationException("The client is not connected.");

            var localId = ++_nextLocalId;
            subscription = new Subscription(
                localId, subscribeMethod, subscribeParams, unsubscribeMethod, sink, oneShot);
            _active.Add(localId, subscription);
            epoch = _connection;
        }

        await AttachCancellationAsync(subscription, cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EstablishAsync(subscription, epoch, initial: true, cancellationToken);
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
        bool initial,
        CancellationToken cancellationToken)
    {
        PendingSubscribe pending;
        lock (_stateGate)
        {
            if (subscription.Phase == SubscriptionPhase.Terminal)
                throw new OperationCanceledException(cancellationToken);
            if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
                throw new InvalidOperationException("The WebSocket connection changed before the subscription was sent.");

            var requestId = ++_nextRequestId;
            pending = new PendingSubscribe(requestId, epoch, subscription, initial);
            subscription.Attempt = pending;
            _pending.Add(requestId, pending);
        }

        try
        {
            await SendAsync(
                epoch,
                new RpcRequest
                {
                    Id = pending.RequestId,
                    Method = subscription.SubscribeMethod,
                    Params = subscription.Params
                },
                cancellationToken);

            await pending.Acked.Task.WaitAsync(_options.SubscriptionAckTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            if (AbandonPendingOrObserveAcknowledged(pending))
                return;

            throw new TimeoutException(
                $"The node did not acknowledge '{subscription.SubscribeMethod}' within {_options.SubscriptionAckTimeout}.",
                exception);
        }
        catch
        {
            if (AbandonPendingOrObserveAcknowledged(pending))
                return;
            throw;
        }
    }

    private bool AbandonPendingOrObserveAcknowledged(PendingSubscribe pending)
    {
        lock (_stateGate)
        {
            if (pending.State == PendingState.Acknowledged)
                return true;

            if (pending.State == PendingState.Awaiting)
            {
                pending.State = PendingState.Abandoned;
                if (ReferenceEquals(pending.Subscription.Attempt, pending))
                    pending.Subscription.Attempt = null;
            }

            // Keep an abandoned request as a generation-scoped tombstone. A late acknowledgement
            // otherwise creates an unowned server-side subscription that can never be released.
            return false;
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
                work.Binding.Value, work.Subscription.UnsubscribeMethod, work.SendReservationHeld);
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
            attempt.State = PendingState.Abandoned;
            subscription.Attempt = null;
            if (exception is OperationCanceledException canceled)
                attempt.Acked.TrySetCanceled(canceled.CancellationToken);
            else
                attempt.Acked.TrySetException(
                    exception ?? new InvalidOperationException("The subscription ended before acknowledgement."));
        }

        var binding = subscription.Binding;
        if (binding is not null)
        {
            _byServerId.Remove((binding.Value.Epoch.Generation, binding.Value.ServerId));
            subscription.Binding = null;
        }

        CancellationTokenRegistration? registration = null;
        if (subscription.HasCancellationRegistration)
        {
            registration = subscription.CancellationRegistration;
            subscription.HasCancellationRegistration = false;
            _cancellationRegistrationCount--;
        }

        var reservationHeld = unsubscribe && binding is not null && TryReserveSendLocked(binding.Value.Epoch);
        return new TerminalWork(subscription, exception, binding, registration, reservationHeld);
    }

    private async Task ExecuteTerminalWorkAsync(TerminalWork work)
    {
        work.Subscription.Sink.Complete(work.Exception);
        if (work.CancellationRegistration is { } registration)
            await registration.DisposeAsync();

        if (work.Binding is not null)
            await SendUnsubscribeReservedAsync(
                work.Binding.Value, work.Subscription.UnsubscribeMethod, work.SendReservationHeld);
    }

    private async Task SendUnsubscribeReservedAsync(
        RouteBinding binding,
        string method,
        bool reservationHeld)
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
                new RpcRequest { Id = requestId, Method = method, Params = [binding.ServerId] },
                _lifetimeCts.Token,
                reservationHeld: true);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(
                exception,
                "Solana WS unsubscribe '{Method}' (id {SubscriptionId}) failed",
                method,
                binding.ServerId);
        }
    }

    private async Task SendAsync(
        ConnectionEpoch epoch,
        RpcRequest request,
        CancellationToken cancellationToken,
        bool reservationHeld = false)
    {
        if (!reservationHeld)
        {
            lock (_stateGate)
            {
                if (!TryReserveSendLocked(epoch))
                    throw new InvalidOperationException("The WebSocket connection changed before the request was sent.");
            }
        }

        try
        {
            var json = JsonSerializer.Serialize(request, RpcJson.TypeInfo<RpcRequest>());
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _lifetimeCts.Token, epoch.Token);
            await _sendLock.WaitAsync(linked.Token);
            try
            {
                lock (_stateGate)
                {
                    if (_phase != ClientPhase.Connected || !ReferenceEquals(_connection, epoch))
                        throw new InvalidOperationException("The WebSocket connection changed before the request was sent.");
                }

                await epoch.Connection.SendAsync(json, linked.Token);
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
            _sendOperationsDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
                var message = await ReceiveWithTimeoutAsync(epoch.Connection, token);
                if (message is null)
                    return null;

                await RouteAsync(message, epoch);
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

    private async ValueTask<string?> ReceiveWithTimeoutAsync(
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
                    if (_phase != ClientPhase.Reconnecting)
                        throw new ObjectDisposedException(nameof(SolanaWsClient));
                    _connecting = candidate;
                }

                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, candidate.Token);
                await candidate.Connection.ConnectAsync(_endpoint!, linked.Token);

                lock (_stateGate)
                {
                    if (_phase != ClientPhase.Reconnecting || !ReferenceEquals(_connecting, candidate))
                        throw new ObjectDisposedException(nameof(SolanaWsClient));

                    _connecting = null;
                    _connection = candidate;
                    _phase = ClientPhase.Connected;
                }

                return candidate;
            }
            catch (OperationCanceledException)
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

                _logger.LogDebug(
                    exception,
                    "Solana WS reconnect attempt {Attempt} failed; retrying in {Delay}",
                    attempt + 1,
                    delay);
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
                if (ReferenceEquals(pending.Subscription.Attempt, pending))
                    pending.Subscription.Attempt = null;
                pending.Acked.TrySetException(exception);
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
                .. _active.Values.Where(subscription =>
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
                await EstablishAsync(subscription, epoch, initial: false, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Solana WS failed to replay subscription '{Method}'",
                    subscription.SubscribeMethod);
            }
        }
    }

    private async Task RouteAsync(string message, ConnectionEpoch epoch)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (root.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var requestId))
        {
            if (root.TryGetProperty("error", out var errorElement))
            {
                CompletePendingError(requestId, epoch, errorElement);
                return;
            }

            if (root.TryGetProperty("result", out var resultElement))
                await CompletePendingResultAsync(requestId, epoch, resultElement);
            return;
        }

        if (!root.TryGetProperty("params", out var paramsElement) ||
            !paramsElement.TryGetProperty("subscription", out var subscriptionElement) ||
            !paramsElement.TryGetProperty("result", out var notification) ||
            !subscriptionElement.TryGetInt64(out var notified))
        {
            return;
        }

        Subscription? subscription;
        TerminalWork? oneShotWork = null;
        lock (_stateGate)
        {
            var key = (epoch.Generation, ServerId: notified);
            if (!_byServerId.TryGetValue(key, out subscription) ||
                subscription.Binding is not { } binding ||
                !ReferenceEquals(binding.Epoch, epoch) ||
                binding.ServerId != notified)
            {
                return;
            }

            if (subscription.IsOneShot)
                oneShotWork = TryTerminateLocked(subscription, exception: null, unsubscribe: false);
        }

        if (oneShotWork is not null)
        {
            try
            {
                subscription.Sink.Deliver(notification);
                subscription.Sink.Complete(exception: null);
            }
            catch (Exception exception)
            {
                subscription.Sink.Complete(exception);
            }

            if (oneShotWork.CancellationRegistration is { } registration)
                await registration.DisposeAsync();
            return;
        }

        try
        {
            subscription.Sink.Deliver(notification);
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

    private async Task CompletePendingResultAsync(
        int requestId,
        ConnectionEpoch epoch,
        JsonElement result)
    {
        RouteBinding? lateBinding = null;
        string? lateUnsubscribeMethod = null;
        var reservationHeld = false;

        lock (_stateGate)
        {
            if (!_pending.TryGetValue(requestId, out var pending) ||
                !ReferenceEquals(pending.Epoch, epoch))
            {
                return;
            }

            _pending.Remove(requestId);
            var wasAwaiting = pending.State == PendingState.Awaiting;
            if (result.ValueKind != JsonValueKind.Number || !result.TryGetInt64(out var subscriptionId))
            {
                pending.State = PendingState.Failed;
                if (ReferenceEquals(pending.Subscription.Attempt, pending))
                    pending.Subscription.Attempt = null;
                if (wasAwaiting)
                    pending.Acked.TrySetException(new InvalidOperationException("The node rejected the subscription."));
                return;
            }

            var canAccept = wasAwaiting &&
                            pending.Subscription.Phase != SubscriptionPhase.Terminal &&
                            ReferenceEquals(pending.Subscription.Attempt, pending) &&
                            _phase == ClientPhase.Connected &&
                            ReferenceEquals(_connection, epoch);
            pending.State = PendingState.Acknowledged;

            if (canAccept)
            {
                var binding = new RouteBinding(epoch, subscriptionId);
                pending.Subscription.Attempt = null;
                pending.Subscription.Binding = binding;
                if (pending.Initial)
                    pending.Subscription.Phase = SubscriptionPhase.Active;
                _byServerId[(epoch.Generation, subscriptionId)] = pending.Subscription;
                pending.Acked.TrySetResult(subscriptionId);
            }
            else
            {
                lateBinding = new RouteBinding(epoch, subscriptionId);
                lateUnsubscribeMethod = pending.Subscription.UnsubscribeMethod;
                reservationHeld = TryReserveSendLocked(epoch);
            }
        }

        if (lateBinding is not null)
            await SendUnsubscribeReservedAsync(
                lateBinding.Value, lateUnsubscribeMethod!, reservationHeld);
    }

    private void CompletePendingError(int requestId, ConnectionEpoch epoch, JsonElement errorElement)
    {
        var detail = errorElement.ValueKind == JsonValueKind.Object &&
                     errorElement.TryGetProperty("message", out var errorMessage) &&
                     errorMessage.ValueKind == JsonValueKind.String
            ? errorMessage.GetString()
            : errorElement.GetRawText();
        var code = errorElement.ValueKind == JsonValueKind.Object &&
                   errorElement.TryGetProperty("code", out var codeElement) &&
                   codeElement.TryGetInt64(out var codeValue)
            ? codeValue
            : 0;

        string method;
        lock (_stateGate)
        {
            if (!_pending.TryGetValue(requestId, out var pending) ||
                !ReferenceEquals(pending.Epoch, epoch))
            {
                return;
            }

            _pending.Remove(requestId);
            method = pending.Subscription.SubscribeMethod;
            var wasAwaiting = pending.State == PendingState.Awaiting;
            pending.State = PendingState.Failed;
            if (ReferenceEquals(pending.Subscription.Attempt, pending))
                pending.Subscription.Attempt = null;
            if (wasAwaiting)
            {
                pending.Acked.TrySetException(
                    new InvalidOperationException(
                        $"The node rejected '{method}' (code {code}): {detail}"));
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
                return new ValueTask(_disposeTask);

            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
        return new ValueTask(completion.Task);
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

    private sealed class PendingSubscribe(
        int requestId,
        ConnectionEpoch epoch,
        Subscription subscription,
        bool initial)
    {
        public int RequestId { get; } = requestId;

        public ConnectionEpoch Epoch { get; } = epoch;

        public Subscription Subscription { get; } = subscription;

        public bool Initial { get; } = initial;

        public TaskCompletionSource<long> Acked { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public PendingState State { get; set; }
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
            _dispose = new Lazy<Task>(DisposeCoreAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public long Generation { get; }

        public IWebSocketConnection Connection { get; }

        public CancellationToken Token => _closed.Token;

        public Task CancelAsync() => _closed.CancelAsync();

        public Task DisposeOnceAsync() => _dispose.Value;

        private async Task DisposeCoreAsync()
        {
            await _closed.CancelAsync();
            await _disposeConnection(Connection);
        }
    }

    private readonly record struct RouteBinding(ConnectionEpoch Epoch, long ServerId);

    private sealed record TerminalWork(
        Subscription Subscription,
        Exception? Exception,
        RouteBinding? Binding,
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
        bool isOneShot)
    {
        public long LocalId { get; } = localId;

        public string SubscribeMethod { get; } = subscribeMethod;

        public object[] Params { get; } = parameters;

        public string UnsubscribeMethod { get; } = unsubscribeMethod;

        public ISubscriptionSink Sink { get; } = sink;

        public bool IsOneShot { get; } = isOneShot;

        public SubscriptionPhase Phase { get; set; } = SubscriptionPhase.Establishing;

        public PendingSubscribe? Attempt { get; set; }

        public RouteBinding? Binding { get; set; }

        public CancellationTokenRegistration CancellationRegistration { get; set; }

        public bool HasCancellationRegistration { get; set; }
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

    private enum PendingState
    {
        Awaiting,
        Abandoned,
        Acknowledged,
        Failed
    }

    private static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private interface ISubscriptionSink
    {
        void Deliver(JsonElement result);

        void Complete(Exception? exception);
    }

    private sealed class SubscriptionSink<T> : ISubscriptionSink
    {
        private readonly Channel<T> _channel;
        private readonly int _capacity;
        private volatile bool _completed;

        // Resolved once per sink so an unregistered notification type fails at subscribe time, not on
        // first delivery deep inside the receive loop.
        private readonly JsonTypeInfo<T> _typeInfo = RpcJson.TypeInfo<T>();

        public SubscriptionSink(int capacity)
        {
            _capacity = capacity;
            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                SingleWriter = false,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            });
        }

        public ChannelReader<T> Reader => _channel.Reader;

        public void Deliver(JsonElement result)
        {
            var value = result.Deserialize(_typeInfo);
            if (value is null || _channel.Writer.TryWrite(value))
                return;

            // TryWrite also fails on a channel that was already completed - a notification racing the
            // subscription's cancellation or fault - which is a benign late delivery, not an overflow.
            if (_completed)
                return;

            throw new InvalidOperationException(
                $"Subscription notification buffer exceeded its capacity of {_capacity} item(s).");
        }

        public void Complete(Exception? exception)
        {
            _completed = true;
            _channel.Writer.TryComplete(exception);
        }
    }
}
