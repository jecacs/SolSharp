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

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(10);
    }
}
