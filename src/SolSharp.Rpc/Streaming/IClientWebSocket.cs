using System.Net.WebSockets;

namespace SolSharp.Rpc.Streaming;

/// <summary>The subset of <see cref="ClientWebSocket"/> used by the transport.</summary>
internal interface IClientWebSocket : IDisposable
{
    WebSocketState State { get; }

    WebSocketCloseStatus? CloseStatus { get; }

    string? CloseStatusDescription { get; }

    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

    ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken);

    ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken);

    Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken);

    Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken);

    void Abort();
}

/// <summary>Adapts the BCL <see cref="ClientWebSocket"/> to <see cref="IClientWebSocket"/>.</summary>
internal sealed class ClientWebSocketAdapter : IClientWebSocket
{
    private readonly ClientWebSocket _socket = new();

    public WebSocketState State => _socket.State;

    public WebSocketCloseStatus? CloseStatus => _socket.CloseStatus;

    public string? CloseStatusDescription => _socket.CloseStatusDescription;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        => _socket.ConnectAsync(uri, cancellationToken);

    public ValueTask SendAsync(
        ReadOnlyMemory<byte> buffer,
        WebSocketMessageType messageType,
        bool endOfMessage,
        CancellationToken cancellationToken)
        => _socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);

    public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken)
        => _socket.ReceiveAsync(buffer, cancellationToken);

    public Task CloseOutputAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
        => _socket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);

    public Task CloseAsync(
        WebSocketCloseStatus closeStatus,
        string? statusDescription,
        CancellationToken cancellationToken)
        => _socket.CloseAsync(closeStatus, statusDescription, cancellationToken);

    public void Abort() => _socket.Abort();

    public void Dispose() => _socket.Dispose();
}
