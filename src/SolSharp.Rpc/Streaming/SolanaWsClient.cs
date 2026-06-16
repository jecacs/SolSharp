using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using SolSharp.Core.Converters;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A multiplexed Solana WebSocket client: every subscription shares one connection and
/// notifications are routed by subscription id. Subscriptions are exposed either as an
/// <see cref="IAsyncEnumerable{T}"/> (which unsubscribes when enumeration ends) or as a
/// <see cref="System.Threading.Channels.ChannelReader{T}"/> (which unsubscribes when its token is cancelled).
/// </summary>
public sealed class SolanaWsClient : IAsyncDisposable
{
    private readonly IWebSocketConnection _connection;
    private readonly ConcurrentDictionary<int, PendingSubscribe> _pending = new();
    private readonly ConcurrentDictionary<long, ISubscriptionSink> _subscriptions = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private int _nextRequestId;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoop;

    /// <summary>Creates a client over a real <see cref="System.Net.WebSockets.ClientWebSocket"/>.</summary>
    public SolanaWsClient() : this(new ClientWebSocketConnection())
    {
    }

    internal SolanaWsClient(IWebSocketConnection connection) => _connection = connection;

    /// <summary>Opens the WebSocket connection and starts the receive loop.</summary>
    /// <param name="endpoint">The WebSocket endpoint (wss://...).</param>
    /// <param name="cancellationToken">A token to cancel the connect.</param>
    /// <returns>A task that completes once connected.</returns>
    /// <exception cref="System.Net.WebSockets.WebSocketException">The connection could not be established.</exception>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        await _connection.ConnectAsync(endpoint, cancellationToken);
        _readLoopCts = new CancellationTokenSource();
        _readLoop = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));
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
        var subscriptionId = await SendSubscribeAsync("logsSubscribe", parameters, sink, cancellationToken);

        cancellationToken.Register(() =>
        {
            if (_subscriptions.TryRemove(subscriptionId, out _))
            {
                sink.Complete(new OperationCanceledException(cancellationToken));
                _ = SendUnsubscribeAsync("logsUnsubscribe", subscriptionId);
            }
        });

        return sink.Reader;
    }

    private async IAsyncEnumerable<T> SubscribeAsync<T>(
        string subscribeMethod,
        object[] subscribeParams,
        string unsubscribeMethod,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var sink = new SubscriptionSink<T>();
        var subscriptionId = await SendSubscribeAsync(subscribeMethod, subscribeParams, sink, cancellationToken);

        try
        {
            await foreach (var item in sink.Reader.ReadAllAsync(cancellationToken))
                yield return item;
        }
        finally
        {
            _subscriptions.TryRemove(subscriptionId, out _);
            await SendUnsubscribeAsync(unsubscribeMethod, subscriptionId);
        }
    }

    private async Task<long> SendSubscribeAsync<T>(
        string method, object[] @params, SubscriptionSink<T> sink, CancellationToken cancellationToken)
    {
        var requestId = Interlocked.Increment(ref _nextRequestId);
        var acked = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[requestId] = new PendingSubscribe(acked, sink);

        await SendAsync(new RpcRequest { Id = requestId, Method = method, Params = @params }, cancellationToken);

        await using (cancellationToken.Register(() => acked.TrySetCanceled(cancellationToken)))
            return await acked.Task;
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
            // best effort: the connection may already be gone
        }
    }

    private async Task SendAsync(RpcRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, SolanaJsonSerializer.Options);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _connection.SendAsync(json, cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await _connection.ReceiveAsync(cancellationToken);
                if (message is null)
                    break;

                Route(message);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception exception)
        {
            FaultAll(exception);
            return;
        }

        FaultAll(new InvalidOperationException("The WebSocket connection was closed."));
    }

    private void Route(string message)
    {
        using var document = JsonDocument.Parse(message);
        var root = document.RootElement;

        // Subscribe/unsubscribe acknowledgement: { "id": <reqId>, "result": <subscriptionId|bool> }.
        if (root.TryGetProperty("id", out var idElement) &&
            root.TryGetProperty("result", out var resultElement) &&
            idElement.TryGetInt32(out var requestId) &&
            _pending.TryRemove(requestId, out var pending))
        {
            if (resultElement.ValueKind == JsonValueKind.Number && resultElement.TryGetInt64(out var subscriptionId))
            {
                _subscriptions[subscriptionId] = pending.Sink;
                pending.Acked.TrySetResult(subscriptionId);
            }
            else
            {
                pending.Acked.TrySetException(new InvalidOperationException("The node rejected the subscription."));
            }

            return;
        }

        // Notification: { "method": "...", "params": { "subscription": <id>, "result": <payload> } }.
        if (root.TryGetProperty("params", out var paramsElement) &&
            paramsElement.TryGetProperty("subscription", out var subscriptionElement) &&
            paramsElement.TryGetProperty("result", out var notification) &&
            subscriptionElement.TryGetInt64(out var notified) &&
            _subscriptions.TryGetValue(notified, out var sink))
        {
            sink.Deliver(notification);
        }
    }

    private void FaultAll(Exception exception)
    {
        foreach (var pending in _pending.Values)
            pending.Acked.TrySetException(exception);
        _pending.Clear();

        foreach (var sink in _subscriptions.Values)
            sink.Complete(exception);
        _subscriptions.Clear();
    }

    /// <summary>Closes the connection and ends all subscriptions.</summary>
    /// <returns>A task that completes once cleanup is done.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_readLoopCts is not null)
            await _readLoopCts.CancelAsync();

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop;
            }
            catch
            {
                // ignore shutdown faults
            }
        }

        await _connection.DisposeAsync();
        _readLoopCts?.Dispose();
        _sendLock.Dispose();
    }

    private readonly record struct PendingSubscribe(TaskCompletionSource<long> Acked, ISubscriptionSink Sink);

    private interface ISubscriptionSink
    {
        void Deliver(JsonElement result);

        void Complete(Exception exception);
    }

    private sealed class SubscriptionSink<T> : ISubscriptionSink
    {
        private readonly Channel<T> _channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions
        {
            SingleWriter = true,
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
