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
    private readonly Lock _lifecycleGate = new();
    private readonly TaskCompletionSource _peerCloseReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Task? _disposeTask;
    private bool _receiveActive;
    private bool _closeOwnsReceive;
    private bool _closeOutputClaimed;
    private bool _disposed;

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

    public ValueTask<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleGate)
        {
            if (_disposed ||
                (_disposeTask is not null &&
                 (_closeOwnsReceive || _peerCloseReceived.Task.IsCompleted)))
            {
                return ValueTask.FromException<string?>(
                    new ObjectDisposedException(nameof(ClientWebSocketConnection)));
            }

            if (_receiveActive)
            {
                return ValueTask.FromException<string?>(
                    new InvalidOperationException("Only one WebSocket receive may be active at a time."));
            }

            _receiveActive = true;
        }

        return ReceiveCoreAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        TaskCompletionSource completion;
        bool receiveLoopOwnsPeerRead;
        lock (_lifecycleGate)
        {
            if (_disposeTask is not null)
                return new(_disposeTask);

            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _disposeTask = completion.Task;
            receiveLoopOwnsPeerRead = _receiveActive;
            _closeOwnsReceive = !receiveLoopOwnsPeerRead;
        }

        _ = DisposeCoreAsync(completion, receiveLoopOwnsPeerRead);
        return new(completion.Task);
    }

    private async ValueTask<string?> ReceiveCoreAsync(CancellationToken cancellationToken)
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
                    // When the peer initiated the close, acknowledge it here. If local disposal already
                    // sent our close frame, ManagedWebSocket is CloseSent/Closed and the handshake is done.
                    if (_socket.State == WebSocketState.CloseReceived)
                    {
                        await CloseOutputSafelyAsync(
                            _socket.CloseStatus ?? WebSocketCloseStatus.NormalClosure,
                            _socket.CloseStatusDescription,
                            cancellationToken);
                    }

                    _peerCloseReceived.TrySetResult();
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
        catch (Exception exception)
        {
            // A disposer waiting for the receive loop to consume the peer's close frame cannot make
            // further progress after a receive failure; wake it so it can abort instead of waiting twice.
            _peerCloseReceived.TrySetException(exception);
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            lock (_lifecycleGate)
                _receiveActive = false;
        }
    }

    private async Task DisposeCoreAsync(TaskCompletionSource completion, bool receiveLoopOwnsPeerRead)
    {
        using var timeout = new CancellationTokenSource();
        timeout.CancelAfter(_closeTimeout);
        Exception? cleanupException = null;
        try
        {
            switch (_socket.State)
            {
                case WebSocketState.Open when receiveLoopOwnsPeerRead:
                    // CloseOutputAsync only sends our half of the handshake. The already-active receive
                    // loop owns the one permitted receive and completes the other half below.
                    if (TryClaimCloseOutput())
                    {
                        await _socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                    }

                    await _peerCloseReceived.Task.WaitAsync(timeout.Token);
                    break;

                case WebSocketState.CloseReceived when receiveLoopOwnsPeerRead:
                case WebSocketState.CloseSent when receiveLoopOwnsPeerRead:
                    await _peerCloseReceived.Task.WaitAsync(timeout.Token);
                    break;

                case WebSocketState.Open:
                case WebSocketState.CloseSent:
                    // With no external receive in flight, let ClientWebSocket own both halves.
                    await _socket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                    break;

                case WebSocketState.CloseReceived:
                    if (TryClaimCloseOutput())
                    {
                        await _socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, "bye", timeout.Token);
                    }

                    break;
            }
        }
        catch
        {
            try
            {
                _socket.Abort();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }
        }
        finally
        {
            try
            {
                _socket.Dispose();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            lock (_lifecycleGate)
                _disposed = true;

            if (cleanupException is null)
                completion.TrySetResult();
            else
                completion.TrySetException(cleanupException);
        }
    }

    private async Task CloseOutputSafelyAsync(
        WebSocketCloseStatus closeStatus,
        string? description,
        CancellationToken cancellationToken)
    {
        if (!TryClaimCloseOutput())
            return;

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

    private bool TryClaimCloseOutput()
    {
        lock (_lifecycleGate)
        {
            if (_closeOutputClaimed)
                return false;

            _closeOutputClaimed = true;
            return true;
        }
    }
}
