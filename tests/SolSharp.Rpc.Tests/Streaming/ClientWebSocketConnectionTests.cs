using System.Net.WebSockets;
using System.Text;
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
        public async Task CloseOutputDoesNotComplete_AbortsAndDisposes()
        {
            // Arrange
            var socket = new FakeClientWebSocket { BlockCloseOutput = true };
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromMilliseconds(20));

            // Act
            await connection.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            socket.AbortCalled.Should().BeTrue();
            socket.DisposeCalled.Should().BeTrue();
        }
    }

    private sealed class FakeClientWebSocket : IClientWebSocket
    {
        private readonly Queue<(byte[] Data, WebSocketMessageType Type, bool EndOfMessage)> _frames = new();

        public WebSocketState State { get; private set; } = WebSocketState.Open;

        public WebSocketCloseStatus? PeerCloseStatus { get; init; } = WebSocketCloseStatus.NormalClosure;

        public string? PeerCloseDescription { get; init; }

        public WebSocketCloseStatus? CloseStatus => PeerCloseStatus;

        public string? CloseStatusDescription => PeerCloseDescription;

        public WebSocketCloseStatus? SentCloseStatus { get; private set; }

        public string? SentCloseDescription { get; private set; }

        public bool BlockCloseOutput { get; init; }

        public bool AbortCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask SendAsync(
            ReadOnlyMemory<byte> buffer,
            WebSocketMessageType messageType,
            bool endOfMessage,
            CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask<ValueWebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            if (_frames.Count == 0)
                throw new InvalidOperationException("The connection read past the queued frames; push more frames or end the message.");

            var frame = _frames.Dequeue();
            frame.Data.CopyTo(buffer);
            if (frame.Type == WebSocketMessageType.Close)
                State = WebSocketState.CloseReceived;

            return ValueTask.FromResult(new ValueWebSocketReceiveResult(frame.Data.Length, frame.Type, frame.EndOfMessage));
        }

        public async Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            SentCloseStatus = closeStatus;
            SentCloseDescription = statusDescription;
            if (BlockCloseOutput)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            State = WebSocketState.CloseSent;
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
            => _frames.Enqueue((data, type, endOfMessage));
    }
}
