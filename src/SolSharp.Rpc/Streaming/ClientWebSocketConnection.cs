using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace SolSharp.Rpc.Streaming;

/// <summary>Real <see cref="IWebSocketConnection"/> backed by a <see cref="ClientWebSocket"/>.</summary>
internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    private const int BufferSize = 32 * 1024;
    private const int DefaultMaxMessageSizeBytes = 64 * 1024 * 1024;

    private static readonly TimeSpan DefaultCloseTimeout = TimeSpan.FromSeconds(5);

    private readonly IClientWebSocket _socket;
    private readonly int _maxMessageSizeBytes;
    private readonly TimeSpan _closeTimeout;

    public ClientWebSocketConnection()
        : this(new ClientWebSocketAdapter(), DefaultMaxMessageSizeBytes, DefaultCloseTimeout)
    {
    }

    internal ClientWebSocketConnection(int maxMessageSizeBytes)
        : this(new ClientWebSocketAdapter(), maxMessageSizeBytes, DefaultCloseTimeout)
    {
    }

    internal ClientWebSocketConnection(IClientWebSocket socket, int maxMessageSizeBytes, TimeSpan closeTimeout)
    {
        ArgumentNullException.ThrowIfNull(socket);
        if (maxMessageSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxMessageSizeBytes), "Maximum message size must be positive.");
        if (closeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(closeTimeout), "Close timeout must be positive.");

        _socket = socket;
        _maxMessageSizeBytes = maxMessageSizeBytes;
        _closeTimeout = closeTimeout;
    }

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        => _socket.ConnectAsync(uri, cancellationToken);

    public ValueTask SendAsync(string text, CancellationToken cancellationToken)
        => _socket.SendAsync(Encoding.UTF8.GetBytes(text).AsMemory(), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    public async ValueTask<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var message = new MemoryStream();
            while (true)
            {
                var result = await _socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseOutputSafelyAsync(
                        _socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                        _socket.CloseStatusDescription,
                        cancellationToken);
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await CloseOutputSafelyAsync(
                        WebSocketCloseStatus.InvalidMessageType,
                        "Only text messages are supported.",
                        cancellationToken);
                    throw new InvalidDataException("Solana WebSocket accepts text messages only.");
                }

                if (message.Length + result.Count > _maxMessageSizeBytes)
                {
                    await CloseOutputSafelyAsync(
                        WebSocketCloseStatus.MessageTooBig,
                        "Message exceeds the configured maximum size.",
                        cancellationToken);
                    throw new InvalidDataException(
                        $"WebSocket message exceeded the configured maximum size of {_maxMessageSizeBytes} bytes.");
                }

                message.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                    break;
            }

            return Encoding.UTF8.GetString(message.ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await CloseOutputSafelyAsync(
                WebSocketCloseStatus.NormalClosure,
                "bye",
                CancellationToken.None);
        }

        _socket.Dispose();
    }

    private async Task CloseOutputSafelyAsync(
        WebSocketCloseStatus closeStatus,
        string? description,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_closeTimeout);
        try
        {
            await _socket.CloseOutputAsync(closeStatus, description, timeout.Token);
        }
        catch
        {
            _socket.Abort();
        }
    }
}
