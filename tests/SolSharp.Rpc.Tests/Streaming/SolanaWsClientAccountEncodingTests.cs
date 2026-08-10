using System.Text.Json;
using System.Threading.Channels;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Streaming;

namespace SolSharp.Rpc.Tests.Streaming;

public static class SolanaWsClientAccountEncodingTests
{
    private static readonly PublicKey TokenProgram = PublicKey.Parse(SolanaProgramIds.TokenProgram);

    private static async Task<string> NextRequestAsync(FakeWebSocketConnection connection)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (connection.SentCount == 0 && DateTime.UtcNow < deadline)
            await Task.Yield();

        connection.SentCount.Should().BeGreaterThan(0);
        return connection.SentSnapshot()[0];
    }

    private static async Task WaitForSentCountAsync(FakeWebSocketConnection connection, int count)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (connection.SentCount < count && DateTime.UtcNow < deadline)
            await Task.Yield();

        connection.SentCount.Should().BeGreaterThanOrEqualTo(count);
    }

    private static int RequestId(string request)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement.GetProperty("id").GetInt32();
    }

    private static string Acknowledgement(int requestId, ulong subscriptionId) =>
        $$"""{"jsonrpc":"2.0","result":{{subscriptionId}},"id":{{requestId}}}""";

    [TestFixture]
    public sealed class SubscribeAccountWithOptionsAsync
    {
        [TestCase(RpcAccountEncoding.Binary, "binary")]
        [TestCase(RpcAccountEncoding.Base58, "base58")]
        [TestCase(RpcAccountEncoding.Base64, "base64")]
        [TestCase(RpcAccountEncoding.JsonParsed, "jsonParsed")]
        [TestCase(RpcAccountEncoding.Base64Zstd, "base64+zstd")]
        public async Task EveryEncoding_UsesExactWireName(RpcAccountEncoding encoding, string expected)
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var subscribe = client.SubscribeAccountWithOptionsAsync(
                TokenProgram,
                new AccountSubscriptionOptions
                {
                    Encoding = encoding,
                    Commitment = Commitment.Finalized
                });
            var request = await NextRequestAsync(fake);

            // Assert
            using var document = JsonDocument.Parse(request);
            var root = document.RootElement;
            root.GetProperty("method").GetString().Should().Be("accountSubscribe");
            root.GetProperty("params")[1].GetProperty("encoding").GetString().Should().Be(expected);
            root.GetProperty("params")[1].GetProperty("commitment").GetString().Should().Be("finalized");

            fake.PushFromServer(Acknowledgement(root.GetProperty("id").GetInt32(), 41));
            _ = await subscribe;
        }

        [Test]
        public async Task Base64ZstdNotification_PreservesEncodedUnion()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeAccountWithOptionsAsync(
                TokenProgram,
                new AccountSubscriptionOptions { Encoding = RpcAccountEncoding.Base64Zstd });
            var request = await NextRequestAsync(fake);
            fake.PushFromServer(Acknowledgement(RequestId(request), 42));
            var reader = await subscribe;

            // Act
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":42,"result":{"context":{"slot":91},"value":{"lamports":7,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":0,"space":3,"data":["KLUv/Q==","base64+zstd"]}}}}""");
            var notification = await reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            notification.Context!.Slot.Should().Be(91);
            var data = notification.Value!.Data.Should().BeOfType<RpcAccountData.Encoded>().Subject;
            data.Encoding.Should().Be(RpcAccountEncoding.Base64Zstd);
            data.EncodedData.Should().Be("KLUv/Q==");
        }

        [Test]
        public async Task NullOptions_ThrowsArgumentNullException()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var act = async () => await client.SubscribeAccountWithOptionsAsync(TokenProgram, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Test]
        public async Task MismatchedNotificationMethod_DropsCorruptGenerationWithoutDelivery()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeAccountWithOptionsAsync(
                TokenProgram,
                new AccountSubscriptionOptions { Encoding = RpcAccountEncoding.Base64 });
            var request = await NextRequestAsync(fake);
            fake.PushFromServer(Acknowledgement(RequestId(request), 44));
            var reader = await subscribe;

            // Act: the payload is deliberately account-shaped; routing must still reject the logs method.
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":44,"result":{"context":{"slot":93},"value":{"lamports":9,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":0,"space":0,"data":["","base64"]}}}}""");
            var read = async () => await reader.ReadAsync();

            // Assert
            (await read.Should().ThrowAsync<ChannelClosedException>())
                .Which.InnerException.Should().BeOfType<InvalidDataException>();
        }

        [Test]
        public async Task NullNotificationResult_FaultsSubscriptionInsteadOfWaitingForever()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeAccountWithOptionsAsync(
                TokenProgram,
                new AccountSubscriptionOptions { Encoding = RpcAccountEncoding.Binary });
            var request = await NextRequestAsync(fake);
            fake.PushFromServer(Acknowledgement(RequestId(request), 45));
            var reader = await subscribe;

            // Act
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":45,"result":null}}""");
            var read = async () => await reader.ReadAsync();

            // Assert
            (await read.Should().ThrowAsync<ChannelClosedException>())
                .Which.InnerException.Should().BeOfType<JsonException>();
        }

        [Test]
        public async Task NullAccountValue_FaultsOnlyAccountWhileSiblingKeepsStreaming()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var accountSubscribe = client.SubscribeAccountWithOptionsAsync(
                TokenProgram,
                new AccountSubscriptionOptions { Encoding = RpcAccountEncoding.Binary });
            var accountRequest = await NextRequestAsync(fake);
            fake.PushFromServer(Acknowledgement(RequestId(accountRequest), 51));
            var accountReader = await accountSubscribe;

            var logsSubscribe = client.SubscribeLogsAsync(TokenProgram);
            await WaitForSentCountAsync(fake, 2);
            var logsRequest = fake.SentSnapshot()[1];
            fake.PushFromServer(Acknowledgement(RequestId(logsRequest), 52));
            var logsReader = await logsSubscribe;

            // Act
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"accountNotification","params":{"subscription":51,"result":{"context":{"slot":94},"value":null}}}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":52,"result":{"context":{"slot":95},"value":{"signature":"live","err":null,"logs":[]}}}}""");

            // Assert
            var accountRead = async () => await accountReader.ReadAsync();
            (await accountRead.Should().ThrowAsync<ChannelClosedException>())
                .Which.InnerException.Should().BeOfType<JsonException>();
            (await logsReader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)))
                .Value!.Signature.Should().Be("live");
        }
    }

    [TestFixture]
    public sealed class SubscribeProgramWithOptionsAsync
    {
        [Test]
        public async Task JsonParsedWithFilters_UsesExactWireAndPreservesParsedUnion()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeProgramWithOptionsAsync(
                TokenProgram,
                new ProgramSubscriptionOptions
                {
                    Encoding = RpcAccountEncoding.JsonParsed,
                    Commitment = Commitment.Processed,
                    Filters = [AccountFilter.DataSize(165)]
                });
            var request = await NextRequestAsync(fake);

            using var document = JsonDocument.Parse(request);
            var root = document.RootElement;
            root.GetProperty("method").GetString().Should().Be("programSubscribe");
            var config = root.GetProperty("params")[1];
            config.GetProperty("encoding").GetString().Should().Be("jsonParsed");
            config.GetProperty("commitment").GetString().Should().Be("processed");
            config.GetProperty("filters")[0].GetProperty("dataSize").GetInt32().Should().Be(165);
            fake.PushFromServer(Acknowledgement(root.GetProperty("id").GetInt32(), 43));
            var reader = await subscribe;

            // Act
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"programNotification","params":{"subscription":43,"result":{"context":{"slot":92},"value":{"pubkey":"11111111111111111111111111111111","account":{"lamports":8,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":0,"space":165,"data":{"program":"spl-token","parsed":{"type":"account"},"space":165}}}}}}""");
            var notification = await reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

            // Assert
            notification.Context!.Slot.Should().Be(92);
            notification.Value!.PublicKey.Should().Be(default(PublicKey));
            var data = notification.Value.Account.Data.Should().BeOfType<RpcAccountData.Parsed>().Subject;
            data.Program.Should().Be("spl-token");
            data.Space.Should().Be(165);
            data.Value.GetProperty("type").GetString().Should().Be("account");
        }

        [Test]
        public async Task NullOptions_ThrowsArgumentNullException()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));

            // Act
            var act = async () => await client.SubscribeProgramWithOptionsAsync(TokenProgram, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Test]
        public async Task ExplicitNullAccount_FaultsOnlyProgramWhileSiblingKeepsStreaming()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var programSubscribe = client.SubscribeProgramWithOptionsAsync(
                TokenProgram,
                new ProgramSubscriptionOptions { Encoding = RpcAccountEncoding.Base64 });
            var programRequest = await NextRequestAsync(fake);
            fake.PushFromServer(Acknowledgement(RequestId(programRequest), 53));
            var programReader = await programSubscribe;

            var logsSubscribe = client.SubscribeLogsAsync(TokenProgram);
            await WaitForSentCountAsync(fake, 2);
            var logsRequest = fake.SentSnapshot()[1];
            fake.PushFromServer(Acknowledgement(RequestId(logsRequest), 54));
            var logsReader = await logsSubscribe;

            // Act
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"programNotification","params":{"subscription":53,"result":{"context":{"slot":96},"value":{"pubkey":"11111111111111111111111111111111","account":null}}}}""");
            fake.PushFromServer(
                """{"jsonrpc":"2.0","method":"logsNotification","params":{"subscription":54,"result":{"context":{"slot":97},"value":{"signature":"live","err":null,"logs":[]}}}}""");

            // Assert
            var programRead = async () => await programReader.ReadAsync();
            (await programRead.Should().ThrowAsync<ChannelClosedException>())
                .Which.InnerException.Should().BeOfType<JsonException>();
            (await logsReader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)))
                .Value!.Signature.Should().Be("live");
        }
    }
}
