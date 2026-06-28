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
        public async Task DeliversNotification_ThenUnsubscribesOnCancel()
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
