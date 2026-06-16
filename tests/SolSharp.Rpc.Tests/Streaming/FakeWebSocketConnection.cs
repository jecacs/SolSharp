using System.Threading.Channels;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests.Streaming;

/// <summary>In-memory <see cref="IWebSocketConnection"/>: records what is sent, replays canned server messages.</summary>
internal sealed class FakeWebSocketConnection : IWebSocketConnection
{
    private readonly Channel<string> _incoming = Channel.CreateUnbounded<string>();

    public List<string> Sent { get; } = [];

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => Task.CompletedTask;

    public ValueTask SendAsync(string text, CancellationToken cancellationToken)
    {
        Sent.Add(text);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _incoming.Reader.ReadAsync(cancellationToken);
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        _incoming.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    public void PushFromServer(string message) => _incoming.Writer.TryWrite(message);

    /// <summary>Simulates the server dropping the connection: the next <see cref="ReceiveAsync"/> returns null.</summary>
    public void Drop() => _incoming.Writer.TryComplete();
}
