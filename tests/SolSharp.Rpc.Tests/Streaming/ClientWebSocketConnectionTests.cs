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
    public sealed class Receive
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
            message!.Value.Text.Should().Be("hello");
            // The fragmented message is five UTF-8 bytes on the wire; the client budgets that number,
            // not a re-measurement of the decoded string.
            message.Value.WireByteCount.Should().Be(5);
        }

        [Test]
        public async Task InvalidUtf8_ChargesTheWireSizeNotTheReplacedDecoding()
        {
            // Arrange: lone 0xFF bytes are invalid UTF-8. Decoding replaces each with U+FFFD, which
            // re-encodes to three bytes, so measuring the decoded string would charge 3x what arrived.
            var socket = new FakeClientWebSocket();
            socket.Push([0xFF, 0xFF, 0xFF, 0xFF], WebSocketMessageType.Text, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 64, TimeSpan.FromMilliseconds(20));

            // Act
            var message = await connection.ReceiveAsync(CancellationToken.None);

            // Assert
            message!.Value.WireByteCount.Should().Be(4);
            Encoding.UTF8.GetByteCount(message.Value.Text).Should().Be(12, "the decoded form is three times larger");
        }

        [Test]
        public async Task MessageExceedsLimit_ClosesConnectionAndThrows()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            socket.Push("hello", WebSocketMessageType.Text, endOfMessage: true);
            await using var connection = new ClientWebSocketConnection(socket, 4, TimeSpan.FromMilliseconds(20));

            // Act
            var act = connection.Awaiting(static subject =>
                subject.ReceiveAsync(CancellationToken.None));

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
            var act = connection.Awaiting(static subject =>
                subject.ReceiveAsync(CancellationToken.None));

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

        [Test]
        public async Task ConcurrentCall_IsRejectedWhileFirstReceiveCompletesNormally()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            await using var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));
            var first = connection.ReceiveAsync(CancellationToken.None).AsTask();
            await socket.ReceiveStarted.Task;

            // Act
            var second = connection.Awaiting(static subject =>
                subject.ReceiveAsync(CancellationToken.None));

            try
            {
                // Assert
                await second.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Only one WebSocket receive may be active at a time.");
            }
            finally
            {
                socket.Push("first", WebSocketMessageType.Text, endOfMessage: true);
            }

            (await first)!.Value.Text.Should().Be("first");
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
        public async Task WithActiveReceiveAndIntermediateText_AllowsNextReceiveToCompleteHandshake()
        {
            // Arrange
            var socket = new FakeClientWebSocket();
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));
            var firstReceive = connection.ReceiveAsync(CancellationToken.None).AsTask();
            await socket.ReceiveStarted.Task;

            // Act: disposal sends its close half, but the active receive may still return text that
            // was already in flight. The receive loop must remain able to read the peer's close next.
            var dispose = connection.DisposeAsync().AsTask();
            await socket.CloseOutputStarted.Task;
            socket.Push("in-flight", WebSocketMessageType.Text, endOfMessage: true);
            (await firstReceive)!.Value.Text.Should().Be("in-flight");

            var secondReceive = connection.ReceiveAsync(CancellationToken.None).AsTask();
            socket.Push([], WebSocketMessageType.Close, endOfMessage: true);

            // Assert
            (await secondReceive).Should().BeNull();
            await dispose.WaitAsync(TimeSpan.FromSeconds(1));
            socket.CloseOutputCallCount.Should().Be(1);
        }

        [Test]
        public async Task AfterPeerCloseWhileDisposeIsFinishing_RejectsAnotherReceive()
        {
            // Arrange
            using var disposeEntered = new ManualResetEventSlim();
            using var releaseDispose = new ManualResetEventSlim();
            var socket = new FakeClientWebSocket
            {
                DisposeEntered = disposeEntered,
                DisposeRelease = releaseDispose
            };
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));
            var receive = connection.ReceiveAsync(CancellationToken.None).AsTask();
            await socket.ReceiveStarted.Task;
            var dispose = connection.DisposeAsync().AsTask();
            try
            {
                await socket.CloseOutputStarted.Task;
                socket.Push([], WebSocketMessageType.Close, endOfMessage: true);
                (await receive).Should().BeNull();
                (await Task.Run(() => disposeEntered.Wait(TimeSpan.FromSeconds(1)))).Should().BeTrue();

                // Act
                var lateReceive = connection.Awaiting(static subject => subject
                    .ReceiveAsync(CancellationToken.None)
                    .AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(1)));

                // Assert
                await lateReceive.Should().ThrowAsync<ObjectDisposedException>();
            }
            finally
            {
                releaseDispose.Set();
                await dispose.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }

        [Test]
        public async Task BackendDisposeThrows_AllCallersObserveTheSharedFailure()
        {
            // Arrange
            var socket = new FakeClientWebSocket
            {
                DisposeException = new InvalidOperationException("backend dispose failed")
            };
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));

            // Act
            var first = connection.DisposeAsync().AsTask();
            var second = connection.DisposeAsync().AsTask();

            // Assert
            first.Should().BeSameAs(second);
            var firstFailure = async () => await first.WaitAsync(TimeSpan.FromSeconds(1));
            var secondFailure = async () => await second.WaitAsync(TimeSpan.FromSeconds(1));
            await firstFailure.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("backend dispose failed");
            await secondFailure.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("backend dispose failed");
        }

        [Test]
        public async Task PeerCloseRacesOpenStateSnapshot_SendsOnlyOneCloseOutput()
        {
            // Arrange
            using var stateReadEntered = new ManualResetEventSlim();
            using var releaseStateRead = new ManualResetEventSlim();
            var socket = new FakeClientWebSocket();
            var connection = new ClientWebSocketConnection(socket, 1024, TimeSpan.FromSeconds(1));
            var receive = connection.ReceiveAsync(CancellationToken.None).AsTask();
            await socket.ReceiveStarted.Task;
            socket.GateNextStateRead(stateReadEntered, releaseStateRead);

            // Dispose captures Open, then pauses before invoking CloseOutputAsync. Meanwhile the receive
            // path observes the peer close and claims the one permitted close-output operation.
            var dispose = Task.Run(() => connection.DisposeAsync().AsTask());
            try
            {
                (await Task.Run(() => stateReadEntered.Wait(TimeSpan.FromSeconds(1)))).Should().BeTrue();
                socket.Push([], WebSocketMessageType.Close, endOfMessage: true);
                (await receive).Should().BeNull();
                await socket.CloseOutputStarted.Task;
            }
            finally
            {
                releaseStateRead.Set();
                await dispose.WaitAsync(TimeSpan.FromSeconds(1));
            }

            // Assert
            socket.CloseOutputCallCount.Should().Be(1);
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

        private int _closeOutputCallCount;
        private int _gateNextStateRead;
        private ManualResetEventSlim? _stateReadEntered;
        private ManualResetEventSlim? _stateReadRelease;

        public FakeClientWebSocket()
        {
            State = WebSocketState.Open;
        }

        public WebSocketState State
        {
            get
            {
                var snapshot = field;
                if (Interlocked.Exchange(ref _gateNextStateRead, 0) != 0)
                {
                    _stateReadEntered!.Set();
                    _stateReadRelease!.Wait();
                }

                return snapshot;
            }

            private set;
        }

        public WebSocketCloseStatus? PeerCloseStatus { get; init; } = WebSocketCloseStatus.NormalClosure;

        public string? PeerCloseDescription { get; init; }

        public WebSocketCloseStatus? CloseStatus => PeerCloseStatus;

        public string? CloseStatusDescription => PeerCloseDescription;

        public WebSocketCloseStatus? SentCloseStatus { get; private set; }

        public string? SentCloseDescription { get; private set; }

        public bool BlockCloseAsync { get; init; }

        public bool CloseAsyncCalled { get; private set; }

        public bool AbortCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public int CloseOutputCallCount => Volatile.Read(ref _closeOutputCallCount);

        public ManualResetEventSlim? DisposeEntered { get; init; }

        public ManualResetEventSlim? DisposeRelease { get; init; }

        public Exception? DisposeException { get; init; }

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

            return new(frame.Data.Length, frame.Type, frame.EndOfMessage);
        }

        public Task CloseOutputAsync(
            WebSocketCloseStatus closeStatus,
            string? statusDescription,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _closeOutputCallCount);
            SentCloseStatus = closeStatus;
            SentCloseDescription = statusDescription;
            CloseOutputStarted.TrySetResult();
            State = State == WebSocketState.CloseReceived
                ? WebSocketState.Closed
                : WebSocketState.CloseSent;
            return Task.CompletedTask;
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

        public void Dispose()
        {
            DisposeEntered?.Set();
            DisposeRelease?.Wait();
            DisposeCalled = true;
            if (DisposeException is not null)
                throw DisposeException;
        }

        public void GateNextStateRead(ManualResetEventSlim entered, ManualResetEventSlim release)
        {
            _stateReadEntered = entered;
            _stateReadRelease = release;
            Volatile.Write(ref _gateNextStateRead, 1);
        }

        public void Push(string text, WebSocketMessageType type, bool endOfMessage)
            => Push(Encoding.UTF8.GetBytes(text), type, endOfMessage);

        public void Push(byte[] data, WebSocketMessageType type, bool endOfMessage)
            => _frames.Writer.TryWrite((data, type, endOfMessage));
    }
}
