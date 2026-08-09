using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Streaming;
using SolSharp.Rpc.Tests.Streaming;

namespace SolSharp.Rpc.Tests;

public static class AccountFilterTests
{
    private const string ProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    [TestFixture]
    public sealed class MemoryCompare
    {
        [Test]
        public void ValidBase58_PreservesLegacyFactoryAndCreatesExactPayload()
        {
            // Arrange
            const string encoded = "3Mc6vR";

            // Act
            var filter = AccountFilter.MemoryCompare(7, encoded);

            // Assert
            var payload = filter.Payload.Should().BeOfType<MemcmpFilter>().Subject.Memcmp;
            payload.Offset.Should().Be(7);
            payload.Bytes.Should().Be(encoded);
            payload.Encoding.Should().Be("base58");
        }

        [Test]
        public void NegativeOffset_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            Action act = () => _ = AccountFilter.MemoryCompare(-1, "1");

            // Act & Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("offset");
        }
    }

    [TestFixture]
    public sealed class MemoryCompareBase58
    {
        [Test]
        public void MaximumDecodedLengthAndOffset_AreAccepted()
        {
            // Arrange
            var encoded = Base58.Encode(Enumerable.Repeat(byte.MaxValue, 128).ToArray());
            encoded.Should().HaveLength(175);

            // Act
            var filter = AccountFilter.MemoryCompareBase58(ulong.MaxValue, encoded);

            // Assert
            var payload = filter.Payload.Should().BeOfType<MemcmpFilter>().Subject.Memcmp;
            payload.Offset.Should().Be(ulong.MaxValue);
            payload.Bytes.Should().Be(encoded);
            payload.Encoding.Should().Be("base58");
        }

        [Test]
        public void InvalidEncoding_ThrowsArgumentException()
        {
            // Arrange
            Action act = () => _ = AccountFilter.MemoryCompareBase58(0, "III");

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithParameterName("bytesBase58");
        }

        [Test]
        public void MoreThan128DecodedBytes_ThrowsArgumentException()
        {
            // Arrange: each leading base58 '1' represents one zero byte, matching the pinned Agave boundary KAT.
            var encoded = new string('1', 129);
            Action act = () => _ = AccountFilter.MemoryCompareBase58(0, encoded);

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithParameterName("bytesBase58");
        }
    }

    [TestFixture]
    public sealed class MemoryCompareBase64
    {
        [Test]
        public void MaximumDecodedLengthAndOffset_AreAccepted()
        {
            // Arrange
            var encoded = Convert.ToBase64String(Enumerable.Repeat(byte.MaxValue, 128).ToArray());
            encoded.Should().HaveLength(172);

            // Act
            var filter = AccountFilter.MemoryCompareBase64(ulong.MaxValue, encoded);

            // Assert
            var payload = filter.Payload.Should().BeOfType<MemcmpFilter>().Subject.Memcmp;
            payload.Offset.Should().Be(ulong.MaxValue);
            payload.Bytes.Should().Be(encoded);
            payload.Encoding.Should().Be("base64");
        }

        [Test]
        public void InvalidEncoding_ThrowsArgumentException()
        {
            // Arrange
            Action act = () => _ = AccountFilter.MemoryCompareBase64(0, "not-base64");

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithParameterName("bytesBase64");
        }

        [Test]
        public void MoreThan128DecodedBytes_ThrowsArgumentException()
        {
            // Arrange
            var encoded = Convert.ToBase64String(new byte[129]);
            Action act = () => _ = AccountFilter.MemoryCompareBase64(0, encoded);

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithParameterName("bytesBase64");
        }
    }

    [TestFixture]
    public sealed class MemoryCompareRaw
    {
        [Test]
        public void MaximumLengthAndOffset_CreateDefensiveRawPayload()
        {
            // Arrange
            var bytes = Enumerable.Range(0, 128).Select(static value => (byte)value).ToArray();

            // Act
            var filter = AccountFilter.MemoryCompareRaw(ulong.MaxValue, bytes);
            bytes[0] = byte.MaxValue;

            // Assert
            var payload = filter.Payload.Should().BeOfType<RawMemcmpFilter>().Subject.Memcmp;
            payload.Offset.Should().Be(ulong.MaxValue);
            payload.Bytes.Should().Equal(Enumerable.Range(0, 128).Select(static value => (byte)value));
            payload.Encoding.Should().Be("bytes");
        }

        [Test]
        public void MoreThan128Bytes_ThrowsArgumentException()
        {
            // Arrange
            Action act = () => _ = AccountFilter.MemoryCompareRaw(0, new byte[129]);

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithParameterName("bytes");
        }
    }

    [TestFixture]
    public sealed class DataSize
    {
        [Test]
        public void PositiveValue_PreservesLegacyFactory()
        {
            // Arrange & Act
            var filter = AccountFilter.DataSize(165);

            // Assert
            filter.Payload.Should().BeOfType<DataSizeFilter>().Subject.DataSize.Should().Be(165);
        }

        [Test]
        public void NegativeValue_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            Action act = () => _ = AccountFilter.DataSize(-1);

            // Act & Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("size");
        }
    }

    [TestFixture]
    public sealed class DataSizeUnsigned
    {
        [Test]
        public void UnsignedMaximum_IsPreserved()
        {
            // Arrange & Act
            var filter = AccountFilter.DataSizeUnsigned(ulong.MaxValue);

            // Assert
            filter.Payload.Should().BeOfType<DataSizeFilter>().Subject.DataSize.Should().Be(ulong.MaxValue);
        }
    }

    [TestFixture]
    public sealed class TokenAccountState
    {
        [Test]
        public void Factory_CreatesExactUnitVariant()
        {
            // Arrange & Act
            var filter = AccountFilter.TokenAccountState();

            // Assert
            filter.Payload.Should().Be("tokenAccountState");
        }
    }

    [TestFixture]
    public sealed class GetProgramAccountsAsync
    {
        [Test]
        public async Task FullFilterUnion_SendsExactPinnedAgaveJson()
        {
            // Arrange
            var handler = new FakeHttpMessageHandler("""{"jsonrpc":"2.0","result":[],"id":1}""");
            using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(http);
            var options = new GetProgramAccountsOptions
            {
                Filters =
                [
                    AccountFilter.MemoryCompareBase58(ulong.MaxValue, "3Mc6vR"),
                    AccountFilter.MemoryCompareBase64(8, "AQID"),
                    AccountFilter.MemoryCompareRaw(9, [0, 1, 2, 255]),
                    AccountFilter.DataSizeUnsigned(ulong.MaxValue),
                    AccountFilter.TokenAccountState()
                ]
            };

            // Act
            var result = await client.GetProgramAccountsAsync(PublicKey.Parse(ProgramId), options);

            // Assert
            result.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getProgramAccounts","params":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA",{"encoding":"base64","filters":[{"memcmp":{"offset":18446744073709551615,"bytes":"3Mc6vR","encoding":"base58"}},{"memcmp":{"offset":8,"bytes":"AQID","encoding":"base64"}},{"memcmp":{"offset":9,"bytes":[0,1,2,255],"encoding":"bytes"}},{"dataSize":18446744073709551615},"tokenAccountState"]}]}""");
        }
    }

    [TestFixture]
    public sealed class SubscribeProgramWithOptionsAsync
    {
        [Test]
        public async Task FullFilterUnion_SendsExactPinnedAgaveJson()
        {
            // Arrange
            var fake = new FakeWebSocketConnection();
            await using var client = new SolanaWsClient(fake);
            await client.ConnectAsync(new Uri("wss://localhost"));
            var subscribe = client.SubscribeProgramWithOptionsAsync(
                PublicKey.Parse(ProgramId),
                new ProgramSubscriptionOptions
                {
                    Encoding = RpcAccountEncoding.Base64,
                    Filters =
                    [
                        AccountFilter.MemoryCompareBase58(ulong.MaxValue, "3Mc6vR"),
                        AccountFilter.MemoryCompareBase64(8, "AQID"),
                        AccountFilter.MemoryCompareRaw(9, [0, 1, 2, 255]),
                        AccountFilter.DataSizeUnsigned(ulong.MaxValue),
                        AccountFilter.TokenAccountState()
                    ]
                });
            var request = await NextRequestAsync(fake);

            // Act
            fake.PushFromServer("""{"jsonrpc":"2.0","result":41,"id":1}""");
            _ = await subscribe;

            // Assert
            request.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"programSubscribe","params":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA",{"encoding":"base64","filters":[{"memcmp":{"offset":18446744073709551615,"bytes":"3Mc6vR","encoding":"base58"}},{"memcmp":{"offset":8,"bytes":"AQID","encoding":"base64"}},{"memcmp":{"offset":9,"bytes":[0,1,2,255],"encoding":"bytes"}},{"dataSize":18446744073709551615},"tokenAccountState"]}]}""");
        }
    }

    private static async Task<string> NextRequestAsync(FakeWebSocketConnection connection)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);
        while (connection.SentCount == 0 && DateTime.UtcNow < deadline)
            await Task.Yield();

        connection.SentCount.Should().BeGreaterThan(0);
        return connection.SentSnapshot()[0];
    }
}
