namespace SolSharp.Rpc.Streaming;

/// <summary>
/// A text WebSocket connection. Abstracted so the streaming client can be tested without a real socket.
/// </summary>
internal interface IWebSocketConnection : IAsyncDisposable
{
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    ValueTask SendAsync(string text, CancellationToken cancellationToken);

    ValueTask<string?> ReceiveAsync(CancellationToken cancellationToken);
}
