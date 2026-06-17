using System.Globalization;
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
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var subscription = client.SubscribeSlotsAsync().GetAsyncEnumerator();
            var move = subscription.MoveNextAsync();

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"slotSubscribe\"");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":42,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"slotNotification","params":{"subscription":42,"result":{"parent":10,"root":9,"slot":11}}}""");

            (await move).Should().BeTrue();
            subscription.Current.Slot.Should().Be(11);
            subscription.Current.Parent.Should().Be(10);

            await subscription.DisposeAsync();

            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"slotUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeLogs
    {
        [Test]
        public async Task DeliversThroughChannel_ThenUnsubscribesOnCancel()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            var subscribe = client.SubscribeLogsAsync(program, cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"logsSubscribe\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram); // program -> base58 in mentions

            fake.PushFromServer("""{"jsonrpc":"2.0","result":7,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":7,"result":{"context":{"slot":100},"value":{"signature":"sig11","err":null,"logs":["Program log: hi"]}}}}""");

            var message = await reader.ReadAsync();
            message.Value!.Signature.Should().Be("sig11");
            message.Value.Logs.Should().ContainSingle().Which.Should().Be("Program log: hi");
            message.Value.IsError.Should().BeFalse();

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(message => message.Contains("logsUnsubscribe")));
            fake.Sent.Should().Contain(message => message.Contains("\"method\":\"logsUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class SubscribeAccount
    {
        [Test]
        public async Task DeliversDecodedAccount_ThenUnsubscribesOnCancel()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var account = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            var subscribe = client.SubscribeAccountAsync(account, cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"accountSubscribe\"");
            fake.Sent[0].Should().Contain("\"base64\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram); // account -> base58

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
            var subscribe = client.SubscribeAccountAsync(account);

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
    }

    [TestFixture]
    public sealed class SubscribeProgram
    {
        [Test]
        public async Task DeliversProgramAccount_ThenUnsubscribesOnCancel()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var program = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            using var cts = new CancellationTokenSource();
            var subscribe = client.SubscribeProgramAsync(program, filters: [AccountFilter.DataSize(165)], cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"programSubscribe\"");
            fake.Sent[0].Should().Contain("\"base64\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram); // program -> base58
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
        public async Task DeliversNotification_ThenUnsubscribesOnCancel()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            var subscribe = client.SubscribeSignatureAsync("Sig111", cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"signatureSubscribe\"");
            fake.Sent[0].Should().Contain("Sig111");

            fake.PushFromServer("""{"jsonrpc":"2.0","result":3,"id":1}""");
            var reader = await subscribe;

            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"signatureNotification","params":{"subscription":3,"result":{"context":{"slot":100},"value":{"err":null}}}}""");

            var message = await reader.ReadAsync();
            message.Value!.IsError.Should().BeFalse();

            await cts.CancelAsync();
            await WaitUntil(() => fake.Sent.Exists(entry => entry.Contains("signatureUnsubscribe")));
            fake.Sent.Should().Contain(entry => entry.Contains("\"method\":\"signatureUnsubscribe\""));
        }
    }

    [TestFixture]
    public sealed class ConfirmSignature
    {
        [Test]
        public async Task ReturnsResultWhenNotified()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            var confirm = client.ConfirmSignatureAsync("Sig111");

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.PushFromServer("""{"jsonrpc":"2.0","result":4,"id":1}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"signatureNotification","params":{"subscription":4,"result":{"context":{"slot":100},"value":{"err":null}}}}""");

            var result = await confirm;

            result.IsError.Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class SubscribeBlocks
    {
        [Test]
        public async Task DeliversBlock_ThenUnsubscribesOnCancel()
        {
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            var subscribe = client.SubscribeBlocksAsync(cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"blockSubscribe\"");
            fake.Sent[0].Should().Contain("\"all\""); // no filter -> "all"
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
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            using var cts = new CancellationTokenSource();
            _ = client.SubscribeBlocksAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram), cancellationToken: cts.Token);

            await WaitUntil(() => fake.Sent.Count > 0);
            fake.Sent[0].Should().Contain("\"method\":\"blockSubscribe\"");
            fake.Sent[0].Should().Contain("\"mentionsAccountOrProgram\"");
            fake.Sent[0].Should().Contain(SolanaProgramIds.TokenProgram); // program -> base58

            await cts.CancelAsync();
        }
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
