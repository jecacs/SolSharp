using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SolSharp.Core.Converters;
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
/// </summary>
public sealed class SolanaWsClient : IAsyncDisposable
{
    private readonly Func<IWebSocketConnection> _connectionFactory;
    private readonly SolanaWsClientOptions _options;
    private readonly ConcurrentDictionary<int, PendingSubscribe> _pending = new();
    private readonly ConcurrentDictionary<long, Subscription> _active = new();
    private readonly ConcurrentDictionary<long, Subscription> _byServerId = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();

    private IWebSocketConnection? _connection;
    private Uri? _endpoint;
    private int _nextRequestId;
    private long _nextLocalId;
    private long _connectionGeneration;
    private Task? _runLoop;

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/> with default options.</summary>
    public SolanaWsClient() : this(new SolanaWsClientOptions())
    {
    }

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/>.</summary>
    /// <param name="options">Connection options, including the auto-reconnect policy.</param>
    public SolanaWsClient(SolanaWsClientOptions options) : this(() => new ClientWebSocketConnection(), options)
    {
    }

    internal SolanaWsClient(Func<IWebSocketConnection> connectionFactory, SolanaWsClientOptions options)
    {
        _connectionFactory = connectionFactory;
        _options = options;
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
    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        _endpoint = endpoint;
        _connection = _connectionFactory();
        await _connection.ConnectAsync(endpoint, cancellationToken);
        Interlocked.Increment(ref _connectionGeneration);
        _runLoop = Task.Run(() => RunAsync(_lifetimeCts.Token));
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
        var sink = new SubscriptionSink<RpcContextValue<LogInfo>>();
        object[] parameters = [new { mentions = new[] { program } }, new { commitment }];
        var subscription = await RegisterAsync("logsSubscribe", parameters, "logsUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        var sink = new SubscriptionSink<RpcContextValue<AccountInfo>>();
        object[] parameters = [account, new { encoding = "base64", commitment }];
        var subscription = await RegisterAsync("accountSubscribe", parameters, "accountUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        var sink = new SubscriptionSink<RpcContextValue<ParsedAccountInfo>>();
        object[] parameters = [account, new { encoding = "jsonParsed", commitment }];
        var subscription = await RegisterAsync("accountSubscribe", parameters, "accountUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        var sink = new SubscriptionSink<RpcContextValue<ProgramAccount>>();
        object[] parameters =
        [
            program,
            new { encoding = "base64", commitment, filters = filters?.Select(filter => filter.Payload).ToArray() }
        ];
        var subscription = await RegisterAsync("programSubscribe", parameters, "programUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        => SubscribeBlocksCoreAsync(new { mentionsAccountOrProgram }, commitment, cancellationToken);

    private async Task<ChannelReader<RpcContextValue<BlockNotification>>> SubscribeBlocksCoreAsync(
        object filter,
        Commitment commitment,
        CancellationToken cancellationToken)
    {
        var sink = new SubscriptionSink<RpcContextValue<BlockNotification>>();
        object[] parameters =
        [
            filter,
            new { commitment, encoding = "json", transactionDetails = "signatures", showRewards = false, maxSupportedTransactionVersion = 0 }
        ];
        var subscription = await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        => SubscribeParsedBlocksCoreAsync(new { mentionsAccountOrProgram }, commitment, cancellationToken);

    private async Task<ChannelReader<RpcContextValue<ParsedBlockNotification>>> SubscribeParsedBlocksCoreAsync(
        object filter,
        Commitment commitment,
        CancellationToken cancellationToken)
    {
        var sink = new SubscriptionSink<RpcContextValue<ParsedBlockNotification>>();
        object[] parameters =
        [
            filter,
            new { commitment, encoding = "jsonParsed", transactionDetails = "full", showRewards = false, maxSupportedTransactionVersion = 0 }
        ];
        var subscription = await RegisterAsync("blockSubscribe", parameters, "blockUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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
        var sink = new SubscriptionSink<RpcContextValue<SignatureNotification>>();
        object[] parameters = [signature, new { commitment }];
        var subscription = await RegisterAsync("signatureSubscribe", parameters, "signatureUnsubscribe", sink, cancellationToken);

        cancellationToken.Register(() => Cancel(subscription, cancellationToken));
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

    private async IAsyncEnumerable<T> SubscribeAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sink = new SubscriptionSink<T>();
        var subscription = await RegisterAsync(subscribeMethod, subscribeParams, unsubscribeMethod, sink, cancellationToken);

        try
        {
            await foreach (var item in sink.Reader.ReadAllAsync(cancellationToken))
                yield return item;
        }
        finally
        {
            if (_active.TryRemove(subscription.LocalId, out _) && subscription.ServerId != 0)
            {
                _byServerId.TryRemove(subscription.ServerId, out _);
                await SendUnsubscribeAsync(unsubscribeMethod, subscription.ServerId);
            }
        }
    }

    private async Task<Subscription> RegisterAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        SubscriptionSink<T> sink,
        CancellationToken cancellationToken)
    {
        var localId = Interlocked.Increment(ref _nextLocalId);
        var subscription = new Subscription(localId, subscribeMethod, subscribeParams, unsubscribeMethod, sink);
        _active[localId] = subscription;

        try
        {
            await EstablishAsync(subscription, cancellationToken);
        }
        catch
        {
            _active.TryRemove(localId, out _);
            throw;
        }

        return subscription;
    }

    // Sends the subscribe request and waits for the server to assign a subscription id. The receive
    // loop must be running concurrently to route the acknowledgement, so this is never awaited from it.
    private async Task EstablishAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var acked = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = new PendingSubscribe(acked, subscription);

        try
        {
            await SendAsync(
                new RpcRequest { Id = requestId, Method = subscription.SubscribeMethod, Params = subscription.Params },
                cancellationToken);

            await using (cancellationToken.Register(() => acked.TrySetCanceled(cancellationToken)))
                await acked.Task;
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    private void Cancel(Subscription subscription, CancellationToken cancellationToken)
    {
        if (!_active.TryRemove(subscription.LocalId, out _))
            return;

        subscription.Sink.Complete(new OperationCanceledException(cancellationToken));

        if (subscription.ServerId != 0)
        {
            _byServerId.TryRemove(subscription.ServerId, out _);
            _ = SendUnsubscribeAsync(subscription.UnsubscribeMethod, subscription.ServerId);
        }
    }

    private async Task SendUnsubscribeAsync(string method, long subscriptionId)
    {
        try
        {
            var requestId = Interlocked.Increment(ref _nextRequestId);
            await SendAsync(new RpcRequest { Id = requestId, Method = method, Params = [subscriptionId] }, CancellationToken.None);
        }
        catch
        {
        }
    }

    private async Task SendAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        var connection = _connection ?? throw new InvalidOperationException("The client is not connected.");
        var json = JsonSerializer.Serialize(request, SolanaJsonSerializer.Options);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await connection.SendAsync(json, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task RunAsync(CancellationToken token)
    {
        while (true)
        {
            Exception? failure;
            try
            {
                failure = await ReceiveUntilClosedAsync(_connection!, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            var reason = failure ?? new InvalidOperationException("The WebSocket connection was closed.");

            FaultPending(reason);

            if (!_options.AutoReconnect || !await TryReconnectAsync(token))
            {
                CompleteAll(reason);
                return;
            }

            // Re-enter the receive loop below so it can route the acks; resubscribe off-thread to avoid a deadlock.
            _ = ResubscribeAllAsync(Volatile.Read(ref _connectionGeneration), token);
        }
    }

    private async Task<Exception?> ReceiveUntilClosedAsync(IWebSocketConnection connection, CancellationToken token)
    {
        try
        {
            while (true)
            {
                var message = await connection.ReceiveAsync(token);
                if (message is null)
                    return null;

                Route(message);
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

    private async Task<bool> TryReconnectAsync(CancellationToken token)
    {
        if (_connection is not null)
            await SafeDisposeAsync(_connection);

        var delay = _options.ReconnectInitialDelay;
        for (var attempt = 0; _options.MaxReconnectAttempts == 0 || attempt < _options.MaxReconnectAttempts; attempt++)
        {
            try
            {
                await Task.Delay(delay, token);
                var connection = _connectionFactory();
                await connection.ConnectAsync(_endpoint!, token);
                _connection = connection;
                Interlocked.Increment(ref _connectionGeneration);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                delay = NextDelay(delay);
            }
        }

        return false;
    }

    private TimeSpan NextDelay(TimeSpan current)
    {
        var doubled = current + current;
        return doubled < _options.ReconnectMaxDelay ? doubled : _options.ReconnectMaxDelay;
    }

    // Replays the established subscriptions onto the freshly reconnected socket, giving each a new server id.
    // A stale replay (a newer reconnect has bumped the generation) bails so it cannot double-subscribe; a
    // failed replay is left in place so the next reconnect retries it rather than dropping the consumer.
    private async Task ResubscribeAllAsync(long generation, CancellationToken token)
    {
        var established = _active.Values.Where(subscription => subscription.Established).ToList();
        _byServerId.Clear();

        foreach (var subscription in established)
            subscription.ServerId = 0;

        foreach (var subscription in established)
        {
            if (token.IsCancellationRequested || Volatile.Read(ref _connectionGeneration) != generation)
                return;

            try
            {
                await EstablishAsync(subscription, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
            }
        }
    }

    private void Route(string message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        if (root.TryGetProperty("id", out var idElement) &&
            root.TryGetProperty("result", out var resultElement) &&
            idElement.TryGetInt32(out var requestId) &&
            _pending.TryRemove(requestId, out var pending))
        {
            if (resultElement.ValueKind == JsonValueKind.Number && resultElement.TryGetInt64(out var subscriptionId))
            {
                pending.Subscription.ServerId = subscriptionId;
                pending.Subscription.Established = true;
                _byServerId[subscriptionId] = pending.Subscription;
                pending.Acked.TrySetResult(subscriptionId);
            }
            else
            {
                pending.Acked.TrySetException(new InvalidOperationException("The node rejected the subscription."));
            }

            return;
        }

        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.TryGetProperty("subscription", out var subscriptionElement) &&
            paramsElement.TryGetProperty("result", out var notification) &&
            subscriptionElement.TryGetInt64(out var notified) &&
            _byServerId.TryGetValue(notified, out var subscription))
        {
            subscription.Sink.Deliver(notification);
        }
    }

    private void FaultPending(Exception exception)
    {
        foreach (var pending in _pending.Values)
            pending.Acked.TrySetException(exception);
        _pending.Clear();
    }

    private void CompleteAll(Exception exception)
    {
        FaultPending(exception);

        foreach (var subscription in _active.Values)
            subscription.Sink.Complete(exception);
        _active.Clear();
        _byServerId.Clear();
    }

    private static async Task SafeDisposeAsync(IWebSocketConnection connection)
    {
        try
        {
            await connection.DisposeAsync();
        }
        catch
        {
        }
    }

    /// <summary>Closes the connection and ends all subscriptions.</summary>
    /// <returns>A task that completes once cleanup is done.</returns>
    public async ValueTask DisposeAsync()
    {
        await _lifetimeCts.CancelAsync();

        if (_runLoop is not null)
        {
            try
            {
                await _runLoop;
            }
            catch
            {
            }
        }

        if (_connection is not null)
            await SafeDisposeAsync(_connection);

        _lifetimeCts.Dispose();
        _sendLock.Dispose();
    }

    private readonly record struct PendingSubscribe(TaskCompletionSource<long> Acked, Subscription Subscription);

    private sealed class Subscription(
        long localId,
        string subscribeMethod,
        object[] parameters,
        string unsubscribeMethod,
        ISubscriptionSink sink)
    {
        public long LocalId { get; } = localId;

        public string SubscribeMethod { get; } = subscribeMethod;

        public object[] Params { get; } = parameters;

        public string UnsubscribeMethod { get; } = unsubscribeMethod;

        public ISubscriptionSink Sink { get; } = sink;

        public long ServerId { get; set; }

        public bool Established { get; set; }
    }

    private interface ISubscriptionSink
    {
        void Deliver(JsonElement result);

        void Complete(Exception exception);
    }

    private sealed class SubscriptionSink<T> : ISubscriptionSink
    {
        private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        public ChannelReader<T> Reader => _channel.Reader;

        public void Deliver(JsonElement result)
        {
            var value = result.Deserialize<T>(SolanaJsonSerializer.Options);
            if (value is not null)
                _channel.Writer.TryWrite(value);
        }

        public void Complete(Exception exception) => _channel.Writer.TryComplete(exception);
    }
}
