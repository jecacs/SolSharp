using System.Globalization;
using System.Threading.Channels;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests.Streaming;

public static class SolanaWsClientTests
{
    [TestFixture]
    public sealed class SubscribeSlots
    {
        [Test]
        public async Task SendsSubscribe_YieldsNotification_ThenUnsubscribes()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeSlotsAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"slotSubscribe\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"slotNotification","params":{"subscription":42,"result":{"parent":10,"root":9,"slot":11}}}""");

            (await move).Should().BeTrue();
            subscription.Current.Slot.Should().Be(11);
            subscription.Current.Parent.Should().Be(10);
            subscription.Current.Root.Should().Be(9);

            await subscription.DisposeAsync();

            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"slotUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeRoots
    {
        [Test]
        public async Task SendsSubscribe_YieldsRoot_ThenUnsubscribes()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeRootsAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"rootSubscribe\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer("""{"jsonrpc":"2.0","method":"rootNotification","params":{"subscription":42,"result":12345}}""");

            (await move).Should().BeTrue();
            subscription.Current.Should().Be(12345ul);

            await subscription.DisposeAsync();
            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"rootUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeVotes
    {
        [Test]
        public async Task SendsSubscribe_YieldsVote_ThenUnsubscribes()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeVotesAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"voteSubscribe\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"voteNotification","params":{"subscription":42,"result":{"votePubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","slots":[249999,250000],"hash":"8Rshv2oMkPu5E4opXTRyuyBeZBqQ4S477VG26wUTFxUM","timestamp":1750000000,"signature":"sig22"}}}""");

            (await move).Should().BeTrue();
            subscription.Current.VotePubkey.Should().Be(PublicKey.Parse("7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67"));
            subscription.Current.Slots.Should().Equal(249999ul, 250000ul);
            subscription.Current.Hash.Should().Be("8Rshv2oMkPu5E4opXTRyuyBeZBqQ4S477VG26wUTFxUM");
            subscription.Current.Timestamp.Should().Be(1750000000L);
            subscription.Current.Signature.Should().Be("sig22");

            await subscription.DisposeAsync();
            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"voteUnsubscribe\""));
        }

        [Test]
        public async Task ParsesNullTimestamp()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeVotesAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"voteNotification","params":{"subscription":42,"result":{"votePubkey":"7QMhYQAPfkoURcrQFxgHKXbipaYL4Sj34kweHx3d3J67","slots":[1],"hash":"h","timestamp":null,"signature":"s"}}}""");

            // Assert
            (await move).Should().BeTrue();
            subscription.Current.Timestamp.Should().BeNull();
            await subscription.DisposeAsync();
        }
    }

    [TestFixture]
    public sealed class SubscribeSlotsUpdates
    {
        [Test]
        public async Task SendsSubscribe_YieldsFrozenUpdateWithStats_ThenUnsubscribes()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeSlotsUpdatesAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"slotsUpdatesSubscribe\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"slotsUpdatesNotification","params":{"subscription":42,"result":{"slot":250001,"type":"frozen","timestamp":1750000000123,"stats":{"numTransactionEntries":96,"numSuccessfulTransactions":120,"numFailedTransactions":6,"maxTransactionsPerEntry":5}}}}""");

            (await move).Should().BeTrue();
            subscription.Current.Slot.Should().Be(250001ul);
            subscription.Current.Type.Should().Be("frozen");
            subscription.Current.Timestamp.Should().Be(1750000000123L);
            subscription.Current.Parent.Should().BeNull();
            subscription.Current.Error.Should().BeNull();
            subscription.Current.Stats!.NumTransactionEntries.Should().Be(96ul);
            subscription.Current.Stats.NumSuccessfulTransactions.Should().Be(120ul);
            subscription.Current.Stats.NumFailedTransactions.Should().Be(6ul);
            subscription.Current.Stats.MaxTransactionsPerEntry.Should().Be(5ul);

            await subscription.DisposeAsync();
            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"slotsUpdatesUnsubscribe\""));
        }

        [Test]
        public async Task ParsesCreatedBankParent_AndDeadError()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscription = client.SubscribeSlotsUpdatesAsync().GetAsyncEnumerator();
            var first = subscription.MoveNextAsync();

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"slotsUpdatesNotification","params":{"subscription":42,"result":{"slot":76,"type":"createdBank","parent":75,"timestamp":1750000000456}}}""");

            // Assert
            (await first).Should().BeTrue();
            subscription.Current.Type.Should().Be("createdBank");
            subscription.Current.Parent.Should().Be(75ul);

            var second = subscription.MoveNextAsync();
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"slotsUpdatesNotification","params":{"subscription":42,"result":{"slot":77,"type":"dead","err":"invalid block","timestamp":1750000000789}}}""");

            (await second).Should().BeTrue();
            subscription.Current.Type.Should().Be("dead");
            subscription.Current.Error.Should().Be("invalid block");

            await subscription.DisposeAsync();
        }
    }

    [TestFixture]
    public sealed class SubscribeLogs
    {
        [Test]
        public async Task DeliversThroughChannel_ThenUnsubscribesOnCancel()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeLogsAsync(program, cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"logsSubscribe\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram);

            fake.PushFromServer("""{"jsonrpc":"2.0","result":7,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":7,"result":{"context":{"slot":100},"value":{"signature":"sig11","err":null,"logs":["Program log: hi"]}}}}""");

            var message = await reader.ReadAsync();
            message.Context!.Slot.Should().Be(100ul);
            message.Value!.Signature.Should().Be("sig11");
            message.Value.Logs.Should().ContainSingle().Which.Should().Be("Program log: hi");
            message.Value.IsError.Should().BeFalse();

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(sent => sent.Contains("logsUnsubscribe")));
            fake.Sent.Should().Contain(sent => sent.Contains("\"method\":\"logsUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeRejection
    {
        [Test]
        public async Task ErrorResponse_FaultsTheSubscribeCall()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            // Act
            var subscribe = client.SubscribeLogsAsync(program);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer(
                """{"jsonrpc":"2.0","error":{"code":-32602,"message":"Too many subscriptions"},"id":1}""");

            // Assert
            var act = async () => await subscribe;
            (await act.Should().ThrowAsync<InvalidOperationException>())
                .Which.Message.Should().Contain("-32602").And.Contain("Too many subscriptions");
        }

        [Test]
        public async Task ErrorResponse_DoesNotDisturbOtherSubscriptions()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            var first = client.SubscribeLogsAsync(program);
            await WaitUntil(() => fake.Sent.Count == 1);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":7,"id":1}""");
            var reader = await first;

            // Act: a second subscribe gets rejected.
            var second = client.SubscribeLogsAsync(program);
            await WaitUntil(() => fake.Sent.Count == 2);
            fake.PushFromServer("""{"jsonrpc":"2.0","error":{"code":-32000,"message":"nope"},"id":2}""");

            var act = async () => await second;
            await act.Should().ThrowAsync<InvalidOperationException>();

            // Assert: the first subscription still delivers.
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":7,"result":{"context":{"slot":100},"value":{"signature":"sig1","err":null,"logs":[]}}}}""");

            var message = await reader.ReadAsync();
            message.Value!.Signature.Should().Be("sig1");
        }
    }

    [TestFixture]
    public sealed class NotificationDecodeFailure
    {
        [Test]
        public async Task FaultsOnlyThatSubscription_OthersKeepStreaming()
        {
            // Arrange: an account and a logs subscription multiplexed over one connection.
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var token = PublicKey.Parse(SolanaProgramIds.TokenProgram);

            var subscribeAccount = client.SubscribeAccountAsync(token);
            await WaitUntil(() => fake.Sent.Count == 1);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":1,"id":1}""");
            var accountReader = await subscribeAccount;

            var subscribeLogs = client.SubscribeLogsAsync(token);
            await WaitUntil(() => fake.Sent.Count == 2);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":2,"id":2}""");
            var logsReader = await subscribeLogs;

            // Act: the account notification is undecodable (no "lamports"); the logs one after it is fine.
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":1,"result":{"context":{"slot":1},"value":{"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":0,"data":["","base64"]}}}}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":2,"result":{"context":{"slot":2},"value":{"signature":"sig22","err":null,"logs":[]}}}}""");

            // Assert: the healthy subscription still delivers over the same, un-dropped connection...
            (await logsReader.ReadAsync()).Value!.Signature.Should().Be("sig22");

            // ...while the broken one is completed with the decode error and unsubscribed.
            var read = async () => await accountReader.ReadAsync();
            (await read.Should().ThrowAsync<ChannelClosedException>())
                .Which.InnerException.Should().NotBeNull();

            await WaitUntil(() => fake.Sent.Exists(entry => entry.Contains("accountUnsubscribe")));
            fake.Sent.Should().Contain(entry => entry.Contains("\"method\":\"accountUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeAccount
    {
        [Test]
        public async Task DeliversDecodedAccount_ThenUnsubscribesOnCancel()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var account = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeAccountAsync(account, cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"accountSubscribe\"");
            fake.Sent[0].Should().Contain("\"base64\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram);

            fake.PushFromServer("""{"jsonrpc":"2.0","result":5,"id":1}""");
            var reader = await subscribe;

            // "AQID" is base64 for the bytes [1, 2, 3].
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":5,"result":{"context":{"slot":100},"value":{"data":["AQID","base64"],"executable":false,"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":18446744073709551615,"space":3}}}}""");

            var message = await reader.ReadAsync();
            byte[] expectedData = [1, 2, 3];
            message.Value!.Lamports.Should().Be(2039280);
            message.Value.Data.Should().Equal(expectedData);

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(entry => entry.Contains("accountUnsubscribe")));
            fake.Sent.Should().Contain(entry => entry.Contains("\"method\":\"accountUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class Reconnect
    {
        [Test]
        public async Task ReplaysSubscriptions_OntoNewConnection_AfterDrop()
        {
            // Arrange
            var first = new FakeWebSocketConnection();
            var second = new FakeWebSocketConnection();
            var connections = new[] { first, second };
            var index = -1;

            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };

            await using var client = new SolanaWsClient(() => connections[Interlocked.Increment(ref index)], options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var account = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            // Act
            var subscribe = client.SubscribeAccountAsync(account);

            // Assert
            await WaitUntil(() => first.Sent.Count > 0);
            first.PushFromServer("""{"jsonrpc":"2.0","result":11,"id":1}""");
            var reader = await subscribe;

            first.PushFromServer(AccountNotification(subscription: 11, lamports: 1));
            (await reader.ReadAsync()).Value!.Lamports.Should().Be(1);

            // Drop the live connection: the client reconnects and replays the subscription onto `second`.
            first.Drop();

            await WaitUntil(() => second.Sent.Exists(message => message.Contains("\"method\":\"accountSubscribe\"")));
            second.PushFromServer("""{"jsonrpc":"2.0","result":22,"id":2}"""); // new server-assigned id

            // A notification carrying the new id reaches the original, still-open reader.
            second.PushFromServer(AccountNotification(subscription: 22, lamports: 2));
            (await reader.ReadAsync()).Value!.Lamports.Should().Be(2);
        }

        [Test]
        public async Task CancelledDuringReplay_UnsubscribesWhenTheAckLands()
        {
            // Arrange: an established subscription, then a drop so the client replays it onto `second`.
            var first = new FakeWebSocketConnection();
            var second = new FakeWebSocketConnection();
            var connections = new[] { first, second };
            var index = -1;
            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };

            await using var client = new SolanaWsClient(() => connections[Interlocked.Increment(ref index)], options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cancellation = new CancellationTokenSource();
            var subscribe = client.SubscribeAccountAsync(
                PublicKey.Parse(SolanaProgramIds.TokenProgram), cancellationToken: cancellation.Token);
            await WaitUntil(() => first.Sent.Count > 0);
            first.PushFromServer("""{"jsonrpc":"2.0","result":11,"id":1}""");
            var reader = await subscribe;

            first.Drop();
            await WaitUntil(() => second.Sent.Exists(message => message.Contains("\"method\":\"accountSubscribe\"")));

            // Act: the consumer goes away while the replayed subscribe is unacknowledged, then the ack
            // lands - the client must release the server-side subscription, not resurrect it.
            cancellation.Cancel();
            await WaitUntil(() => reader.Completion.IsCompleted);
            second.PushFromServer("""{"jsonrpc":"2.0","result":22,"id":2}""");

            // Assert
            await WaitUntil(() => second.Sent.Exists(message => message.Contains("\"method\":\"accountUnsubscribe\"")));
            second.Sent.Last(message => message.Contains("accountUnsubscribe")).Should().Contain("[22]");
        }

        [Test]
        public async Task Dispose_AbortsAndDisposesAReconnectCandidate()
        {
            // Arrange: the reconnect transport ignores cancellation and only returns when disposed.
            var first = new FakeWebSocketConnection();
            var second = new FakeWebSocketConnection();
            second.ConnectBehavior = _ => second.DisposeStarted.Task;
            var index = -1;
            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };
            var client = new SolanaWsClient(
                () => Interlocked.Increment(ref index) == 0 ? first : second,
                options);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => first.SentCount == 1);
            first.PushFromServer(Acknowledgement(RequestId(first.SentSnapshot()[0]), subscriptionId: 8));
            var reader = await subscribe;
            first.Drop();
            await WaitUntil(() => second.ConnectCount == 1);

            // Act
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            second.DisposeCount.Should().Be(1);
            reader.Completion.IsCompletedSuccessfully.Should().BeTrue(
                "Dispose must win over reconnect failure and complete active channels gracefully");
        }

        [Test]
        public async Task SecondDrop_DoesNotLetStaleReplayClearNewGenerationRoutes()
        {
            // Arrange
            var first = new FakeWebSocketConnection();
            var second = new FakeWebSocketConnection();
            var third = new FakeWebSocketConnection();
            var connections = new[] { first, second, third };
            var index = -1;
            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };
            await using var client = new SolanaWsClient(
                () => connections[Interlocked.Increment(ref index)], options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var accountA = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            var accountB = PublicKey.Parse("11111111111111111111111111111111");
            var subscribeA = client.SubscribeAccountAsync(accountA);
            await WaitUntil(() => first.SentCount == 1);
            first.PushFromServer(Acknowledgement(RequestId(first.SentSnapshot()[0]), subscriptionId: 11));
            var readerA = await subscribeA;

            var subscribeB = client.SubscribeAccountAsync(accountB);
            await WaitUntil(() => first.SentCount == 2);
            first.PushFromServer(Acknowledgement(RequestId(first.SentSnapshot()[1]), subscriptionId: 12));
            var readerB = await subscribeB;

            // Generation two starts replaying A, but drops before its ACK. Its replay must be joined and
            // generation-scoped before generation three publishes routes using the same server IDs.
            first.Drop();
            await WaitUntil(() => second.SentCount == 1);
            second.Drop();
            await WaitUntil(() => third.SentCount == 1);

            // Act: acknowledge both third-generation replay requests, deliberately reusing ids 11 and 12.
            AcknowledgeAccountRequest(third, third.SentSnapshot()[0], accountA, serverIdA: 11, serverIdB: 12);
            await WaitUntil(() => third.SentCount == 2);
            AcknowledgeAccountRequest(third, third.SentSnapshot()[1], accountA, serverIdA: 11, serverIdB: 12);

            third.PushFromServer(AccountNotification(subscription: 11, lamports: 101));
            third.PushFromServer(AccountNotification(subscription: 12, lamports: 202));

            // Assert
            (await readerA.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1))).Value!.Lamports.Should().Be(101);
            (await readerB.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1))).Value!.Lamports.Should().Be(202);
        }
    }

    [TestFixture]
    public sealed class ReconnectGiveUp
    {
        [Test]
        public async Task ExhaustedAttempts_CompleteSubscriptionsWithTheError()
        {
            // Arrange: the first connection works; every reconnect attempt fails.
            var first = new FakeWebSocketConnection();
            var attempts = 0;
            var options = new SolanaWsClientOptions
            {
                MaxReconnectAttempts = 2,
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };

            await using var client = new SolanaWsClient(
                () => Interlocked.Increment(ref attempts) == 1
                    ? first
                    : throw new InvalidOperationException("connection refused"),
                options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => first.Sent.Count > 0);
            first.PushFromServer("""{"jsonrpc":"2.0","result":1,"id":1}""");
            var reader = await subscribe;

            // Act: drop the connection; both reconnect attempts fail, so the client gives up.
            first.Drop();
            await WaitUntil(() => reader.Completion.IsCompleted);

            // Assert
            reader.Completion.IsFaulted.Should().BeTrue();
            attempts.Should().Be(3); // the initial connect plus the two failed reconnects
        }

        [Test]
        public async Task FailedReconnect_DisposesTheCandidateSocket()
        {
            // Arrange
            var first = new FakeWebSocketConnection();
            var failed = new FakeWebSocketConnection
            {
                ConnectBehavior = _ => Task.FromException(new InvalidOperationException("connection refused"))
            };
            var index = -1;
            var options = new SolanaWsClientOptions
            {
                MaxReconnectAttempts = 1,
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1)
            };
            await using var client = new SolanaWsClient(
                () => Interlocked.Increment(ref index) == 0 ? first : failed,
                options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => first.Sent.Count > 0);
            first.PushFromServer("""{"jsonrpc":"2.0","result":1,"id":1}""");
            var reader = await subscribe;

            // Act
            first.Drop();
            var completion = async () => await reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            await completion.Should().ThrowAsync<InvalidOperationException>();
            failed.DisposeCount.Should().Be(1);
        }
    }

    [TestFixture]
    public sealed class SubscribeProgram
    {
        [Test]
        public async Task DeliversProgramAccount_ThenUnsubscribesOnCancel()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeProgramAsync(program, filters: [AccountFilter.DataSize(165)], cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"programSubscribe\"");
            fake.Sent[0].Should().Contain("\"base64\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram);
            fake.Sent[0].Should().Contain("\"dataSize\":165");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":9,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(ProgramNotification(subscription: 9, lamports: 7));

            var message = await reader.ReadAsync();
            message.Value!.PublicKey.Should().Be(PublicKey.Parse("11111111111111111111111111111111"));
            message.Value.Account.Lamports.Should().Be(7);

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(entry => entry.Contains("programUnsubscribe")));
            fake.Sent.Should().Contain(entry => entry.Contains("\"method\":\"programUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeSignature
    {
        [Test]
        public async Task DeliversOneNotification_ThenCompletesWithoutReplayableState()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeSignatureAsync("Sig111", cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"signatureSubscribe\"");
            fake.Sent[0].Should().Contain("Sig111");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":3,"id":1}""");
            var reader = await subscribe;
            client.RetainedCancellationRegistrationCount.Should().Be(1);

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"signatureNotification","params":{"subscription":3,"result":{"context":{"slot":100},"value":{"err":null}}}}""");

            var message = await reader.ReadAsync();
            message.Value!.IsError.Should().BeFalse();

            await reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
            reader.TryRead(out _).Should().BeFalse();
            client.RetainedCancellationRegistrationCount.Should().Be(0);
            fake.Sent.Should().NotContain(entry => entry.Contains("signatureUnsubscribe"),
                "the Solana node automatically removes signature subscriptions after their notification");
        }

        [Test]
        public async Task CancellationBeforeDequeuedNotification_WinsWithoutMixedChannelOutcome()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            using var cancellation = new CancellationTokenSource();
            var subscribe = client.SubscribeSignatureAsync("Sig111", cancellationToken: cancellation.Token);
            await WaitUntil(() => fake.SentCount == 1);
            fake.PushFromServer(Acknowledgement(RequestId(fake.SentSnapshot()[0]), subscriptionId: 3));
            var reader = await subscribe;

            var notificationDequeued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fake.ReceiveMessageBehavior = async (message, _) =>
            {
                if (!message.Contains("signatureNotification"))
                    return;
                notificationDequeued.TrySetResult();
                await releaseNotification.Task;
            };

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"signatureNotification","params":{"subscription":3,"result":{"context":{"slot":100},"value":{"err":null}}}}""");
            await notificationDequeued.Task;

            // Act
            await cancellation.CancelAsync();
            releaseNotification.TrySetResult();

            // Assert
            var completion = async () => await reader.Completion.WaitAsync(TimeSpan.FromSeconds(1));
            await completion.Should().ThrowAsync<OperationCanceledException>();
            reader.TryRead(out _).Should().BeFalse();
            client.RetainedCancellationRegistrationCount.Should().Be(0);
            await WaitUntil(() => fake.SentSnapshot().Any(message => message.Contains("signatureUnsubscribe")));
        }
    }

    [TestFixture]
    public sealed class ConfirmSignature
    {
        [Test]
        public async Task ReturnsResultWhenNotified()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var confirm = client.ConfirmSignatureAsync("Sig111");

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":4,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"signatureNotification","params":{"subscription":4,"result":{"context":{"slot":100},"value":{"err":null}}}}""");

            var result = await confirm;

            // Assert
            result.IsError.Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class SubscribeBlocks
    {
        [Test]
        public async Task DeliversBlock_ThenUnsubscribesOnCancel()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeBlocksAsync(cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"blockSubscribe\"");
            fake.Sent[0].Should().Contain("\"all\"");
            fake.Sent[0].Should().Contain("\"transactionDetails\":\"signatures\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":8,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"blockNotification","params":{"subscription":8,"result":{"context":{"slot":100},"value":{"slot":100,"err":null,"block":{"blockhash":"Ckt","previousBlockhash":"Prev","parentSlot":99,"blockHeight":90,"blockTime":1700000000,"signatures":["sig1","sig2"]}}}}}""");

            var message = await reader.ReadAsync();
            message.Value!.Slot.Should().Be(100);
            message.Value.IsError.Should().BeFalse();
            message.Value.Block!.ParentSlot.Should().Be(99);
            message.Value.Block.Signatures.Should().Equal("sig1", "sig2");

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(entry => entry.Contains("blockUnsubscribe")));
            fake.Sent.Should().Contain(entry => entry.Contains("\"method\":\"blockUnsubscribe\""));
        }

        [Test]
        public async Task MentionsFilter_SendsAccount()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            // Act
            _ = client.SubscribeBlocksAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram), cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"blockSubscribe\"");
            fake.Sent[0].Should().Contain("\"mentionsAccountOrProgram\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram);

            await cts.CancelAsync();
        }
    }

    [TestFixture]
    public sealed class SubscribeParsedBlocks
    {
        [Test]
        public async Task DeliversParsedBlock_WithDecodedInstructions()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeParsedBlocksAsync(cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"blockSubscribe\"");
            fake.Sent[0].Should().Contain("\"encoding\":\"jsonParsed\"");
            fake.Sent[0].Should().Contain("\"transactionDetails\":\"full\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":9,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(NotificationJson);

            var message = await reader.ReadAsync();
            message.Value!.Slot.Should().Be(120);
            message.Value.IsError.Should().BeFalse();

            var block = message.Value.Block!;
            block.ParentSlot.Should().Be(119);
            var tx = block.Transactions.Should().ContainSingle().Subject;
            tx.Signatures.Should().ContainSingle().Which.Should().Be("psig1");
            tx.Message.Instructions[0].Parsed!.Type.Should().Be("transfer");

            await cts.CancelAsync();
        }

        private const string NotificationJson =
            """{"jsonrpc":"2.0","method":"blockNotification","params":{"subscription":9,"result":{"context":{"slot":120},"value":{"slot":120,"err":null,"block":{"blockhash":"Pblk1111111111111111111111111111111111111111","previousBlockhash":"Pprev111111111111111111111111111111111111111","parentSlot":119,"blockHeight":100,"blockTime":1700000010,"transactions":[{"transaction":{"signatures":["psig1"],"message":{"accountKeys":[{"pubkey":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","signer":true,"writable":true,"source":"transaction"},{"pubkey":"11111111111111111111111111111111","signer":false,"writable":false,"source":"transaction"}],"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"lamports":7}},"stackHeight":null}],"recentBlockhash":"Prbh1111111111111111111111111111111111111111"}},"meta":null,"version":"legacy"}]}}}}}""";
    }

    [TestFixture]
    public sealed class SubscribeParsedAccount
    {
        [Test]
        public async Task DeliversDecodedTokenAccount()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            // Act
            var subscribe = client.SubscribeParsedAccountAsync(
                PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"), cancellationToken: cts.Token);

            // Assert
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"accountSubscribe\"");
            fake.Sent[0].Should().Contain("\"encoding\":\"jsonParsed\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":11,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(AccountNotificationJson);

            var message = await reader.ReadAsync();
            message.Value!.Program.Should().Be("spl-token");
            message.Value.Parsed!.Type.Should().Be("account");

            await cts.CancelAsync();
        }

        private const string AccountNotificationJson =
            """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":11,"result":{"context":{"slot":250},"value":{"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":18446744073709551615,"space":165,"data":{"program":"spl-token","parsed":{"type":"account","info":{"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","tokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"},"state":"initialized"}},"space":165}}}}}""";
    }

    [TestFixture]
    public sealed class SubscriptionBuffer
    {
        [Test]
        public async Task CapacityExceeded_FaultsSubscription()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            var options = new SolanaWsClientOptions { SubscriptionBufferCapacity = 1 };
            await using var client = new SolanaWsClient(() => fake, options);
            await client.ConnectAsync(new Uri("wss://localhost"));
            await using var subscription = client.SubscribeSlotsAsync().GetAsyncEnumerator();
            var firstMove = subscription.MoveNextAsync();
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("{\"jsonrpc\":\"2.0\",\"result\":42,\"id\":1}");
            fake.PushFromServer(
                "{\"jsonrpc\":\"2.0\",\"method\":\"slotNotification\",\"params\":{\"subscription\":42,\"result\":{\"parent\":10,\"root\":9,\"slot\":11}}}");
            (await firstMove).Should().BeTrue();

            // Act
            fake.PushFromServer(
                "{\"jsonrpc\":\"2.0\",\"method\":\"slotNotification\",\"params\":{\"subscription\":42,\"result\":{\"parent\":11,\"root\":10,\"slot\":12}}}");
            fake.PushFromServer(
                "{\"jsonrpc\":\"2.0\",\"method\":\"slotNotification\",\"params\":{\"subscription\":42,\"result\":{\"parent\":12,\"root\":11,\"slot\":13}}}");
            await WaitUntil(() => fake.Sent.Exists(message => message.Contains("\"method\":\"slotUnsubscribe\"")));
            (await subscription.MoveNextAsync()).Should().BeTrue();
            var finalMove = subscription.MoveNextAsync().AsTask();
            var act = async () => await finalMove;

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*capacity*");
        }
    }

    [TestFixture]
    public sealed class ReceiveTimeout
    {
        [Test]
        public async Task SilentConnection_Reconnects()
        {
            // Arrange
            var first = new FakeWebSocketConnection();
            var second = new FakeWebSocketConnection();
            var index = -1;
            var options = new SolanaWsClientOptions
            {
                ReceiveTimeout = TimeSpan.FromMilliseconds(20),
                ReconnectInitialDelay = TimeSpan.FromMilliseconds(1),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(1),
                MaxReconnectAttempts = 1
            };
            await using var client = new SolanaWsClient(
                () => Interlocked.Increment(ref index) == 0 ? first : second,
                options);

            // Act
            await client.ConnectAsync(new Uri("wss://localhost"));
            await WaitUntil(() => second.ConnectCount >= 1);

            // Assert: the silent first connection triggered a reconnect. MaxReconnectAttempts is a
            // per-drop budget, so the equally silent second connection keeps cycling - the exact count
            // is timing-dependent and deliberately not asserted.
            second.ConnectCount.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [TestFixture]
    public sealed class Constructor
    {
        [Test]
        public void NonPositiveMessageLimit_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { MaxMessageSizeBytes = 0 };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void NonPositiveBufferCapacity_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { SubscriptionBufferCapacity = 0 };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void NonPositiveReceiveTimeout_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { ReceiveTimeout = TimeSpan.Zero };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void NegativeReconnectAttempts_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { MaxReconnectAttempts = -1 };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void NegativeReconnectDelay_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { ReconnectInitialDelay = TimeSpan.FromMilliseconds(-1) };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void ZeroReconnectDelays_AreAcceptedForImmediateRetry()
        {
            // Arrange
            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.Zero,
                ReconnectMaxDelay = TimeSpan.Zero
            };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void ReconnectMaximumBelowInitial_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions
            {
                ReconnectInitialDelay = TimeSpan.FromSeconds(2),
                ReconnectMaxDelay = TimeSpan.FromSeconds(1)
            };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void NonPositiveSubscriptionAckTimeout_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var options = new SolanaWsClientOptions { SubscriptionAckTimeout = TimeSpan.Zero };

            // Act
            Action act = () => _ = new SolanaWsClient(options);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class Connect
    {
        [Test]
        public async Task SecondCall_Throws()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act & Assert
            var second = client.ConnectAsync(new Uri("wss://localhost"));
            var act = async () => await second;
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task AfterDispose_Throws()
        {
            // Arrange
            var client = new SolanaWsClient(new FakeWebSocketConnection());
            await client.DisposeAsync();

            // Act & Assert
            var act = () => client.ConnectAsync(new Uri("wss://localhost"));
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Test]
        public async Task ConcurrentCalls_StartOnlyOneConnectionAndRejectTheOther()
        {
            // Arrange
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new FakeWebSocketConnection { ConnectBehavior = _ => gate.Task };
            await using var client = new SolanaWsClient(fake);

            // Act
            var first = client.ConnectAsync(new Uri("wss://localhost"));
            await WaitUntil(() => fake.ConnectCount == 1);
            var second = client.ConnectAsync(new Uri("wss://localhost"));
            gate.TrySetResult();
            await first;

            // Assert
            var act = async () => await second;
            await act.Should().ThrowAsync<InvalidOperationException>();
            fake.ConnectCount.Should().Be(1);
        }

        [Test]
        public async Task FailedInitialConnection_DisposesCreatedSocket()
        {
            // Arrange
            var fake = new FakeWebSocketConnection
            {
                ConnectBehavior = _ => Task.FromException(new InvalidOperationException("connection refused"))
            };
            await using var client = new SolanaWsClient(() => fake, new SolanaWsClientOptions());

            // Act
            var connect = client.ConnectAsync(new Uri("wss://localhost"));
            var act = async () => await connect;

            // Assert
            await act.Should().ThrowAsync<InvalidOperationException>();
            fake.DisposeCount.Should().Be(1);
        }

        [Test]
        public async Task Dispose_CancelsAndDisposesAHangingInitialConnection()
        {
            // Arrange
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new FakeWebSocketConnection
            {
                ConnectBehavior = async cancellationToken =>
                {
                    entered.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
            };
            var client = new SolanaWsClient(fake);
            var connect = client.ConnectAsync(new Uri("wss://localhost"));
            await entered.Task;

            // Act
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            var act = async () => await connect;
            await act.Should().ThrowAsync<ObjectDisposedException>();
            fake.DisposeCount.Should().Be(1);
        }

        [Test]
        public async Task UserCancellation_PreservesTheOriginalToken()
        {
            // Arrange
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new FakeWebSocketConnection
            {
                ConnectBehavior = async cancellationToken =>
                {
                    entered.TrySetResult();
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
            };
            await using var client = new SolanaWsClient(fake);
            using var cancellation = new CancellationTokenSource();
            var connect = client.ConnectAsync(new Uri("wss://localhost"), cancellation.Token);
            await entered.Task;

            // Act
            await cancellation.CancelAsync();

            // Assert
            var act = async () => await connect;
            var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
            thrown.Which.CancellationToken.Should().Be(cancellation.Token);
        }

        [Test]
        public async Task Dispose_AbortsInitialConnectionThatOnlyReactsToSocketDisposal()
        {
            // Arrange: this deliberately ignores the cancellation token. Disposing the candidate is the
            // only action that releases ConnectAsync.
            var releaseConnect = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new FakeWebSocketConnection
            {
                ConnectBehavior = _ => releaseConnect.Task,
                DisposeBehavior = () =>
                {
                    releaseConnect.TrySetResult();
                    return ValueTask.CompletedTask;
                }
            };
            var client = new SolanaWsClient(fake);
            var connect = client.ConnectAsync(new Uri("wss://localhost"));
            await WaitUntil(() => fake.ConnectCount == 1);

            // Act
            await client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            var act = async () => await connect;
            await act.Should().ThrowAsync<ObjectDisposedException>();
            fake.DisposeCount.Should().Be(1);
        }
    }

    [TestFixture]
    public sealed class SubscribeAcknowledgementTimeout
    {
        [Test]
        public async Task MissingAcknowledgement_FaultsSubscribeWithinConfiguredBound()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            var options = new SolanaWsClientOptions
            {
                AutoReconnect = false,
                SubscriptionAckTimeout = TimeSpan.FromMilliseconds(30)
            };
            await using var client = new SolanaWsClient(() => fake, options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => fake.Sent.Count > 0);

            // Assert
            var act = async () => await subscribe;
            await act.Should().ThrowAsync<TimeoutException>();
        }

        [Test]
        public async Task LateAcknowledgement_AfterTimeout_IsImmediatelyUnsubscribed()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            var options = new SolanaWsClientOptions
            {
                AutoReconnect = false,
                SubscriptionAckTimeout = TimeSpan.FromMilliseconds(20)
            };
            await using var client = new SolanaWsClient(() => fake, options);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => fake.SentCount == 1);
            var requestId = RequestId(fake.SentSnapshot()[0]);
            var act = async () => await subscribe;
            await act.Should().ThrowAsync<TimeoutException>();

            // Act: the server accepted the request, but replied after the local timeout won.
            fake.PushFromServer(Acknowledgement(requestId, subscriptionId: 77));

            // Assert
            await WaitUntil(() => fake.SentSnapshot().Any(message => message.Contains("logsUnsubscribe")));
            fake.SentSnapshot()
                .Should().Contain(message => message.Contains("\"method\":\"logsUnsubscribe\"") && message.Contains("[77]"));
        }
    }

    [TestFixture]
    public sealed class Dispose
    {
        [Test]
        public async Task CompletesActiveSubscriptionChannels()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":1,"id":1}""");
            var reader = await subscribe;

            // Act
            await client.DisposeAsync();

            // Assert: the channel completes (without an error), so a blocked consumer observes the end
            // of the stream rather than hanging forever.
            reader.Completion.IsCompletedSuccessfully.Should().BeTrue();
        }

        [Test]
        public async Task FaultsASubscribeAwaitingItsAcknowledgement()
        {
            // Arrange: the subscribe request is sent but the server never acknowledges it.
            var fake = new FakeWebSocketConnection();
            var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscribe = client.SubscribeLogsAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));
            await WaitUntil(() => fake.Sent.Count > 0);

            // Act
            await client.DisposeAsync();

            // Assert
            var act = async () => await subscribe;
            await act.Should().ThrowAsync<ObjectDisposedException>();
        }

        [Test]
        public async Task CanBeCalledTwice()
        {
            // Arrange
            var client = new SolanaWsClient(new FakeWebSocketConnection());
            await client.ConnectAsync(new Uri("wss://localhost"));
            await client.DisposeAsync();

            // Act & Assert
            var act = async () => await client.DisposeAsync();
            await act.Should().NotThrowAsync();
        }

        [Test]
        public async Task ConcurrentCalls_WaitForTheSameCleanup()
        {
            // Arrange
            var releaseDispose = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var fake = new FakeWebSocketConnection
            {
                DisposeBehavior = async () => await releaseDispose.Task
            };
            var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var first = client.DisposeAsync().AsTask();
            await fake.DisposeStarted.Task;
            var second = client.DisposeAsync().AsTask();

            // Assert: idempotence means sharing the cleanup, not returning before it finishes.
            first.IsCompleted.Should().BeFalse();
            second.IsCompleted.Should().BeFalse();
            releaseDispose.TrySetResult();
            await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(1));
            fake.DisposeCount.Should().Be(1);
        }

        [Test]
        public async Task WaitsForQueuedUnsubscribeBeforeDisposingSendState()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            using var cancellation = new CancellationTokenSource();
            var subscribe = client.SubscribeLogsAsync(
                PublicKey.Parse(SolanaProgramIds.TokenProgram), cancellationToken: cancellation.Token);
            await WaitUntil(() => fake.SentCount == 1);
            fake.PushFromServer(Acknowledgement(RequestId(fake.SentSnapshot()[0]), subscriptionId: 9));
            _ = await subscribe;

            var unsubscribeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseUnsubscribe = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fake.SendBehavior = async (message, _) =>
            {
                if (!message.Contains("logsUnsubscribe"))
                    return;
                unsubscribeEntered.TrySetResult();
                await releaseUnsubscribe.Task;
            };

            await cancellation.CancelAsync();
            await unsubscribeEntered.Task;

            // Act
            var dispose = client.DisposeAsync().AsTask();

            // Assert
            dispose.IsCompleted.Should().BeFalse();
            releaseUnsubscribe.TrySetResult();
            await dispose.WaitAsync(TimeSpan.FromSeconds(1));
            fake.DisposeCount.Should().Be(1);
        }
    }

    private static int RequestId(string request)
    {
        using var document = System.Text.Json.JsonDocument.Parse(request);
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private static string Acknowledgement(int requestId, long subscriptionId) =>
        $$"""{"jsonrpc":"2.0","result":{{subscriptionId}},"id":{{requestId}}}""";

    private static void AcknowledgeAccountRequest(
        FakeWebSocketConnection connection,
        string request,
        PublicKey accountA,
        long serverIdA,
        long serverIdB)
    {
        using var document = System.Text.Json.JsonDocument.Parse(request);
        var root = document.RootElement;
        var subscribedAccount = root.GetProperty("params")[0].GetString();
        var serverId = subscribedAccount == accountA.ToString() ? serverIdA : serverIdB;
        connection.PushFromServer(Acknowledgement(root.GetProperty("id").GetInt32(), serverId));
    }

    // A plain (non-interpolated) raw string so the four trailing literal braces stay content; the two
    // values are substituted afterwards (an interpolated raw string cannot mix {{ }} holes with }}}} here).
    private static string AccountNotification(long subscription, ulong lamports) =>
        """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":__SUB__,"result":{"context":{"slot":1},"value":{"data":["","base64"],"executable":false,"lamports":__LAMP__,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":0}}}}"""
            .Replace("__SUB__", subscription.ToString(CultureInfo.InvariantCulture))
            .Replace("__LAMP__", lamports.ToString(CultureInfo.InvariantCulture));

    private static string ProgramNotification(long subscription, ulong lamports) =>
        """{"jsonrpc":"2.0","method":"programNotification","params":{"subscription":__SUB__,"result":{"context":{"slot":1},"value":{"pubkey":"11111111111111111111111111111111","account":{"data":["AQID","base64"],"executable":false,"lamports":__LAMP__,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":3}}}}}"""
            .Replace("__SUB__", subscription.ToString(CultureInfo.InvariantCulture))
            .Replace("__LAMP__", lamports.ToString(CultureInfo.InvariantCulture));

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(10);
    }
}
