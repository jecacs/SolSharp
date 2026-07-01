using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientNonceTests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    // The 80-byte bincode layout the System program stores (agave nonce state): u32 Versions tag
    // (1 = Current), u32 State tag (1 = Initialized), authority (32), durable nonce (32), fee u64.
    private static byte[] NonceData()
    {
        var data = new byte[NonceAccount.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 1);
        Array.Fill(data, (byte)3, 8, 32);
        Array.Fill(data, (byte)8, 40, 32);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(72), 5000);
        return data;
    }

    [TestFixture]
    public sealed class NonceAccountDecode
    {
        [Test]
        public void DecodesInitializedNonce()
        {
            // Act
            var nonce = NonceAccount.Decode(NonceData());

            // Assert
            nonce.Should().NotBeNull();
            nonce!.Version.Should().Be(1u);
            nonce.Authority.Should().Be(Pk(3));
            nonce.Nonce.Should().Be(Pk(8).ToString());
            nonce.LamportsPerSignature.Should().Be(5000ul);
        }

        [Test]
        public void UninitializedState_ReturnsNull()
        {
            // Arrange: zero the State tag - a created-but-never-initialized nonce account.
            var data = NonceData();
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(4), 0);

            // Act & Assert
            NonceAccount.Decode(data).Should().BeNull();
        }

        [Test]
        public void TooShort_ReturnsNull()
            => NonceAccount.Decode(NonceData().AsSpan(0, NonceAccount.Length - 1)).Should().BeNull();
    }

    [TestFixture]
    public sealed class GetNonceAccountAsync
    {
        [Test]
        public async Task FetchesAndDecodes()
        {
            // Arrange
            var envelope =
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["__DATA__","base64"],"executable":false,"lamports":1447680,"owner":"11111111111111111111111111111111","rentEpoch":0,"space":80}},"id":1}"""
                    .Replace("__DATA__", Convert.ToBase64String(NonceData()));
            var handler = new FakeHttpMessageHandler(envelope);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(http);

            // Act
            var nonce = await client.GetNonceAccountAsync(Pk(2));

            // Assert
            nonce.Should().NotBeNull();
            nonce!.Authority.Should().Be(Pk(3));
            nonce.Nonce.Should().Be(Pk(8).ToString());
            handler.CapturedRequestBody.Should().Contain("getAccountInfo");
        }

        [Test]
        public async Task MissingAccount_ReturnsNull()
        {
            // Arrange
            const string envelope = """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":null},"id":1}""";
            var http = new HttpClient(new FakeHttpMessageHandler(envelope)) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(http);

            // Act & Assert
            (await client.GetNonceAccountAsync(Pk(2))).Should().BeNull();
        }
    }
}
