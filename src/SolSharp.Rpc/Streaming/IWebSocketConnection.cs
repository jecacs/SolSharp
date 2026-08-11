namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A text WebSocket connection. Abstracted so the streaming client can be tested without a real socket.
/// </summary>
internal interface IWebSocketConnection : IAsyncDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    ValueTask SendAsync(string text, CancellationToken cancellationToken);

    ValueTask<WebSocketTextMessage?> ReceiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A received text message and the number of bytes it occupied on the wire. The wire size travels with the
/// decoded text because it is the quantity the transport bounds and the client budgets; re-measuring the
/// decoded string instead over-counts, since UTF-8 decoding replaces each invalid byte with U+FFFD and
/// that re-encodes to three bytes.
/// </summary>
/// <param name="Text">The decoded message text.</param>
/// <param name="WireByteCount">The number of UTF-8 bytes received for this message.</param>
internal readonly record struct WebSocketTextMessage(string Text, int WireByteCount);
