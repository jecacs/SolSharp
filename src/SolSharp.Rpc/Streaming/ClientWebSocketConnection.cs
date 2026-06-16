using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace SolSharp.Rpc.Streaming;

/// <summary>Real <see cref="IWebSocketConnection"/> backed by a <see cref="ClientWebSocket"/>.</summary>
internal sealed class ClientWebSocketConnection : IWebSocketConnection
{
    private const int BufferSize = 32 * 1024;

    private readonly ClientWebSocket _socket = new();

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
                    return null;

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
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch
            {
                // best effort
            }
        }

        _socket.Dispose();
    }
}
