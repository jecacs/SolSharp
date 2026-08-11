using System.Threading.Channels;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests.Streaming;

/// <summary>In-memory <see cref="IWebSocketConnection"/>: records what is sent, replays canned server messages.</summary>
internal sealed class FakeWebSocketConnection : IWebSocketConnection
{
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();
    private int _connectCount;
    private int _disposeCount;

    public List<string> Sent { get; } = [];

    public int SentCount
    {
        get
        {
            lock (Sent)
                return Sent.Count;
        }
    }

    public int ConnectCount => Volatile.Read(ref _connectCount);

    public int DisposeCount => Volatile.Read(ref _disposeCount);

    public TaskCompletionSource DisposeStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource ReceiveStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken LastReceiveCancellationToken { get; private set; }

    public Func<CancellationToken, Task>? ConnectBehavior { get; set; }

    public Func<string, CancellationToken, ValueTask>? SendBehavior { get; set; }

    public Func<string, CancellationToken, ValueTask>? ReceiveMessageBehavior { get; set; }

    public Func<ValueTask>? DisposeBehavior { get; set; }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _connectCount);
        return ConnectBehavior?.Invoke(cancellationToken) ?? Task.CompletedTask;
    }

    public async ValueTask SendAsync(string text, CancellationToken cancellationToken)
    {
        if (SendBehavior is not null)
            await SendBehavior(text, cancellationToken);

        lock (Sent)
            Sent.Add(text);
    }

    public async ValueTask<WebSocketTextMessage?> ReceiveAsync(CancellationToken cancellationToken)
    {
        LastReceiveCancellationToken = cancellationToken;
        ReceiveStarted.TrySetResult();
        try
        {
            var message = await _incoming.Reader.ReadAsync(cancellationToken);
            if (ReceiveMessageBehavior is not null)
                await ReceiveMessageBehavior(message, cancellationToken);

            // Mirrors the real transport: the wire size is the UTF-8 length of what the peer sent.
            return new WebSocketTextMessage(message, System.Text.Encoding.UTF8.GetByteCount(message));
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref _disposeCount);
        DisposeStarted.TrySetResult();
        _incoming.Writer.TryComplete();
        if (DisposeBehavior is not null)
            await DisposeBehavior();
    }

    public void PushFromServer(string message) => _incoming.Writer.TryWrite(message);

    public string[] SentSnapshot()
    {
        lock (Sent)
            return [.. Sent];
    }

    /// <summary>Simulates the server dropping the connection: the next <see cref="ReceiveAsync"/> returns null.</summary>
    public void Drop() => _incoming.Writer.TryComplete();
}
