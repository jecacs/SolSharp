using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientChainTests
{
    private const string TokenProgram = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string SystemProgram = "11111111111111111111111111111111";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetSlotLeadersAsync
    {
        [Test]
        public async Task ParsesLeadersAndSendsRange()
        {
            // Arrange
            var (client, handler) = Make(
                $$"""{"jsonrpc":"2.0","result":["{{TokenProgram}}","{{SystemProgram}}"],"id":1}""");

            // Act
            var leaders = await client.GetSlotLeadersAsync(100, 2);

            // Assert
            leaders.Should().HaveCount(2);
            leaders[0].Should().Be(PublicKey.Parse(TokenProgram));
            leaders[1].Should().Be(PublicKey.Parse(SystemProgram));
            handler.CapturedRequestBody.Should().Contain("\"getSlotLeaders\"");
            handler.CapturedRequestBody.Should().Contain("100");
        }
    }

    [TestFixture]
    public sealed class GetSupplyAsync
    {
        [Test]
        public async Task ParsesSupply()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"total":1000,"circulating":800,"nonCirculating":200,"nonCirculatingAccounts":[]}},"id":1}""");

            // Act
            var supply = await client.GetSupplyAsync();

            // Assert
            supply.Total.Should().Be(1000);
            supply.Circulating.Should().Be(800);
            supply.NonCirculating.Should().Be(200);
        }
    }

    [TestFixture]
    public sealed class GetTokenLargestAccountsAsync
    {
        [Test]
        public async Task ParsesAccounts()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"address":"11111111111111111111111111111111","amount":"500","decimals":6,"uiAmountString":"0.0005"}]},"id":1}""");

            // Act
            var accounts = await client.GetTokenLargestAccountsAsync(PublicKey.Parse(TokenProgram));

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Address.Should().Be(PublicKey.Parse(SystemProgram));
            accounts[0].Amount.Should().Be("500");
            accounts[0].Decimals.Should().Be(6);
            handler.CapturedRequestBody.Should().Contain("\"getTokenLargestAccounts\"");
        }
    }

    [TestFixture]
    public sealed class GetBlockAsync
    {
        [Test]
        public async Task ParsesBlock()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"blockhash":"Ckt","previousBlockhash":"Prev","parentSlot":99,"blockHeight":90,"blockTime":1700000000,"signatures":["sig1","sig2"]},"id":1}""");

            // Act
            var block = await client.GetBlockAsync(100);

            // Assert
            block.Should().NotBeNull();
            block!.Blockhash.Should().Be("Ckt");
            block.PreviousBlockhash.Should().Be("Prev");
            block.ParentSlot.Should().Be(99);
            block.BlockHeight.Should().Be(90);
            block.BlockTime.Should().Be(1700000000);
            block.Signatures.Should().Equal("sig1", "sig2");
            handler.CapturedRequestBody.Should().Contain("\"getBlock\"");
            handler.CapturedRequestBody.Should().Contain("\"transactionDetails\":\"signatures\"");
        }

        [Test]
        public async Task ReturnsNullForSkippedSlot()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":null,"id":1}""");

            // Act
            var block = await client.GetBlockAsync(100);

            // Assert
            block.Should().BeNull();
        }
    }
}
