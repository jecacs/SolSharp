using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests.Streaming;

public static class ClientWebSocketConnectionTests
{
    [TestFixture]
    public sealed class ReceiveAsync
    {
        [Test]
        public async Task FragmentedTextWithinLimit_ReturnsCompleteMessage()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            socket.Push("hel", WebSocketMessageType.Text, endOfMessage: false);
            socket.Push("lo", WebSocketMessageType.Text, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 5, TimeSpan.FromMilliseconds(20));

            // Act
            var message = await connection.ReceiveAsync(CancellationToken.None);

            // Assert
            message.Should().Be("hello");
        }

        [Test]
        public async Task MessageExceedsLimit_ClosesConnectionAndThrows()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            socket.Push("hello", WebSocketMessageType.Text, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 4, TimeSpan.FromMilliseconds(20));

            // Act
            var act = async () => await connection.ReceiveAsync(CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*maximum size*");
            socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.MessageTooBig);
        }

        [Test]
        public async Task BinaryFrame_ClosesConnectionAndThrows()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            socket.Push("{}", WebSocketMessageType.Binary, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));

            // Act
            var act = async () => await connection.ReceiveAsync(CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<InvalidDataException>()
                .WithMessage("*text messages*");
            socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.InvalidMessageType);
        }

        [Test]
        public async Task CloseFrame_AcknowledgesCloseAndReturnsNull()
        {
            // Arrange
            var socket = new FakeClientWebSocket
            {
                PeerCloseStatus = WebSocketCloseStatus.EndpointUnavailable,
                PeerCloseDescription = "maintenance"
            };
            socket.Push([], WebSocketMessageType.Close, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));

            // Act
            var message = await connection.ReceiveAsync(CancellationToken.None);

            // Assert
            message.Should().BeNull();
            socket.SentCloseStatus.Should().Be(WebSocketCloseStatus.EndpointUnavailable);
            socket.SentCloseDescription.Should().Be("maintenance");
        }
    }

    [TestFixture]
    public sealed class DisposeAsync
    {
        [Test]
        public async Task CloseHandshakeDoesNotComplete_AbortsAndDisposes()
        {
            // Arrange
            var socket = new FakeClientWebSocket { BlockCloseAsync = true };
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));

            // Act
            await connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            socket.AbortCalled.Should().BeTrue();
            socket.DisposeCalled.Should().BeTrue();
        }

        [Test]
        public async Task WithoutActiveReceive_UsesFullCloseHandshake()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));

            // Act
            await connection.DisposeAsync();

            // Assert
            socket.CloseAsyncCalled.Should().BeTrue();
            socket.AbortCalled.Should().BeFalse();
            socket.DisposeCalled.Should().BeTrue();
        }

        [Test]
        public async Task WithActiveReceive_SendsCloseAndWaitsForPeerClose()
        {
            // Arrange: the existing receive owns the only legal read from the WebSocket.
            var socket = new FakeClientWebSocket();
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));
            var receive = connection.ReceiveAsync(CancellationToken.None).AsTask();
            await socket.ReceiveStarted.Task;

            // Act
            var dispose = connection.DisposeAsync().AsTask();
            await socket.CloseOutputStarted.Task;

            // Assert: CloseOutput is only the first half; disposal waits for the receive loop to
            // consume the peer's close instead of disposing the socket immediately.
            dispose.IsCompleted.Should().BeFalse();
            socket.CloseAsyncCalled.Should().BeFalse();

            socket.Push([], WebSocketMessageType.Close, endOfMessage: true);
            (await receive).Should().BeNull();
            await dispose.WaitAsync(TimeSpan.FromSeconds(1));

            socket.AbortCalled.Should().BeFalse();
            socket.DisposeCalled.Should().BeTrue();
        }

        [Test]
        public async Task WithActiveReceiveAndSilentPeer_TimesOutAndAborts()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));
            using var receiveCancellation = new CancellationTokenSource();
            var receive = connection.ReceiveAsync(receiveCancellation.Token).AsTask();
            await socket.ReceiveStarted.Task;

            // Act
            await connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert: the one handshake timeout also bounds the active-receive path.
            socket.AbortCalled.Should().BeTrue();
            socket.DisposeCalled.Should().BeTrue();

            await receiveCancellation.CancelAsync();
            var receiveFailure = async () => await receive;
            await receiveFailure.Should().ThrowAsync<OperationCanceledException>();
        }
    }

    private sealed class FakeClientWebSocket : IClientWebSocket
    {
        private readonly Channel<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _frames =
            Channel.CreateUnbounded<(byte[], WebSocketMessageType, bool)>();

        public WebSocketState State { get; private set; } = WebSocketState.Open;

        public WebSocketCloseStatus? PeerCloseStatus { get; init; } = WebSocketCloseStatus.NormalClosure;

        public string? PeerCloseDescription { get; init; }

        public WebSocketCloseStatus? CloseStatus => PeerCloseStatus;

        public string? CloseStatusDescription => PeerCloseDescription;

        public WebSocketCloseStatus? SentCloseStatus { get; private set; }

        public string? SentCloseDescription { get; private set; }

        public bool BlockCloseOutput { get; init; }

        public bool BlockCloseAsync { get; init; }

        public bool CloseAsyncCalled { get; private set; }

        public bool AbortCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public TaskCompletionSource ReceiveStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource CloseOutputStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            ReceiveStarted.TrySetResult();
            var frame = await _frames.Reader.ReadAsync(cancellationToken);
            frame.Data.CopyTo(buffer);
            if (frame.Type == WebSocketMessageType.Close)
                State = State == WebSocketState.CloseSent
                    ? WebSocketState.Closed
                    : WebSocketState.CloseReceived;

            return new ValueWebSocketReceiveResult(frame.Data.Length, frame.Type, frame.EndOfMessage);
        }

        public async Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            SentCloseStatus = closeStatus;
            SentCloseDescription = statusDescription;
            CloseOutputStarted.TrySetResult();
            if (BlockCloseOutput)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            State = State == WebSocketState.CloseReceived
                ? WebSocketState.Closed
                : WebSocketState.CloseSent;
        }

        public async Task CloseAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            CloseAsyncCalled = true;
            SentCloseStatus = closeStatus;
            SentCloseDescription = statusDescription;
            if (BlockCloseAsync)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            State = WebSocketState.Closed;
        }

        public void Abort()
        {
            AbortCalled = true;
            State = WebSocketState.Aborted;
        }

        public void Dispose() => DisposeCalled = true;

        public void Push(string text, WebSocketMessageType type, bool endOfMessage)
            => Push(Encoding.UTF8.GetBytes(text), type, endOfMessage);

        public void Push(byte[] data, WebSocketMessageType type, bool endOfMessage)
            => _frames.Writer.TryWrite((data, type, endOfMessage));
    }
}
