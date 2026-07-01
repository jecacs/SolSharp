using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientClusterTests
{
    private const string Account = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetEpochInfoAsync
    {
        [Test]
        public async Task ParsesEpochInfo()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"absoluteSlot":200,"blockHeight":190,"epoch":5,"slotIndex":40,"slotsInEpoch":432000,"transactionCount":12345},"id":1}""");

            // Act
            var info = await client.GetEpochInfoAsync();

            // Assert
            info.AbsoluteSlot.Should().Be(200);
            info.BlockHeight.Should().Be(190);
            info.Epoch.Should().Be(5);
            info.SlotIndex.Should().Be(40);
            info.SlotsInEpoch.Should().Be(432000);
            info.TransactionCount.Should().Be(12345);
            handler.CapturedRequestBody.Should().Contain("\"getEpochInfo\"");
        }
    }

    [TestFixture]
    public sealed class IsBlockhashValidAsync
    {
        [Test]
        public async Task ReturnsValueAndSendsBlockhash()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":true},"id":1}""");

            // Act
            var valid = await client.IsBlockhashValidAsync(Blockhash);

            // Assert
            valid.Should().BeTrue();
            handler.CapturedRequestBody.Should().Contain("\"isBlockhashValid\"");
            handler.CapturedRequestBody.Should().Contain(Blockhash);
        }
    }

    [TestFixture]
    public sealed class GetFeeForMessageAsync
    {
        [Test]
        public async Task ReturnsFeeAndBase64EncodesMessage()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":5000},"id":1}""");
            byte[] message = [1, 2, 3];

            // Act
            var fee = await client.GetFeeForMessageAsync(message);

            // Assert
            fee.Should().Be(5000);
            handler.CapturedRequestBody.Should().Contain("\"getFeeForMessage\"");
            handler.CapturedRequestBody.Should().Contain(Convert.ToBase64String(message));
        }

        [Test]
        public async Task ReturnsNullWhenBlockhashExpired()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":null},"id":1}""");

            // Act
            var fee = await client.GetFeeForMessageAsync([9]);

            // Assert
            fee.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class RequestAirdropAsync
    {
        [Test]
        public async Task ReturnsSignatureAndSendsLamports()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":"AirdropSig111","id":1}""");

            // Act
            var signature = await client.RequestAirdropAsync(PublicKey.Parse(Account), 1_000_000_000);

            // Assert
            signature.Should().Be("AirdropSig111");
            handler.CapturedRequestBody.Should().Contain("\"requestAirdrop\"");
            handler.CapturedRequestBody.Should().Contain(Account);
            handler.CapturedRequestBody.Should().Contain("1000000000");
        }
    }
}
