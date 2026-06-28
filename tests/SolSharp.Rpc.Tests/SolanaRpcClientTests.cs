using System.Net;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientTests
{
    private static SolanaRpcClient Client(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var http = new HttpClient(new FakeHttpMessageHandler(responseJson, statusCode))
        {
            BaseAddress = new Uri("http://localhost")
        };
        return new SolanaRpcClient(http);
    }

    [TestFixture]
    public sealed class GetLatestBlockhash
    {
        [Test]
        public async Task ParsesBlockhashAndHeightFromContextValue()
        {
            // Arrange
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":328},"value":{"blockhash":"EkSnNWid2cvwEVnVx9aBqawnmiCNiDgp3gUdkDPTKN1N","lastValidBlockHeight":3090}},"id":1}""");

            // Act
            var result = await client.GetLatestBlockhashAsync();

            // Assert
            result.Blockhash.Should().Be("EkSnNWid2cvwEVnVx9aBqawnmiCNiDgp3gUdkDPTKN1N");
            result.LastValidBlockHeight.Should().Be(3090);
        }
    }

    [TestFixture]
    public sealed class GetBalance
    {
        [Test]
        public async Task ParsesLamportsFromContextValue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":1000000000},"id":1}""");

            // Act & Assert
            (await client.GetBalanceAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram)))
                .Should().Be(1_000_000_000);
        }
    }

    [TestFixture]
    public sealed class GetSlot
    {
        [Test]
        public async Task ParsesBareValue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":348543210,"id":1}""");

            // Act & Assert
            (await client.GetSlotAsync()).Should().Be(348543210);
        }
    }

    [TestFixture]
    public sealed class GetHealth
    {
        [Test]
        public async Task Ok_ReturnsTrue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":"ok","id":1}""");

            // Act & Assert
            (await client.GetHealthAsync()).Should().BeTrue();
        }

        [Test]
        public async Task NonOk_ReturnsFalse()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":"behind","id":1}""");

            // Act & Assert
            (await client.GetHealthAsync()).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class GetVersion
    {
        [Test]
        public async Task ParsesSolanaCore()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":{"solana-core":"2.0.14","feature-set":3241752847},"id":1}""");

            // Act & Assert
            (await client.GetVersionAsync()).SolanaCore.Should().Be("2.0.14");
        }
    }

    [TestFixture]
    public sealed class GetBlockHeight
    {
        [Test]
        public async Task ParsesBareValue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":348543200,"id":1}""");

            // Act & Assert
            (await client.GetBlockHeightAsync()).Should().Be(348543200);
        }
    }

    [TestFixture]
    public sealed class GetTokenSupply
    {
        [Test]
        public async Task ParsesAmountAndDecimals()
        {
            // Arrange
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"amount":"1000000000","decimals":6,"uiAmount":1000.0,"uiAmountString":"1000"}},"id":1}""");

            // Act
            var supply = await client.GetTokenSupplyAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));

            // Assert
            supply.Amount.Should().Be("1000000000");
            supply.Decimals.Should().Be(6);
        }
    }

    [TestFixture]
    public sealed class GetMinimumBalanceForRentExemption
    {
        [Test]
        public async Task ParsesBareValue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":2039280,"id":1}""");

            // Act & Assert
            (await client.GetMinimumBalanceForRentExemptionAsync(165)).Should().Be(2039280);
        }
    }

    [TestFixture]
    public sealed class GetTransactionCount
    {
        [Test]
        public async Task ParsesBareValue()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","result":268,"id":1}""");

            // Act & Assert
            (await client.GetTransactionCountAsync()).Should().Be(268);
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountBalance
    {
        [Test]
        public async Task ParsesAmountAndDecimals()
        {
            // Arrange
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"amount":"9864","decimals":2,"uiAmount":98.64,"uiAmountString":"98.64"}},"id":1}""");

            // Act
            var balance = await client.GetTokenAccountBalanceAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));

            // Assert
            balance.Amount.Should().Be("9864");
            balance.Decimals.Should().Be(2);
        }
    }

    [TestFixture]
    public sealed class Errors
    {
        [Test]
        public async Task NodeError_ThrowsRpcExceptionWithCode()
        {
            // Arrange
            var client = Client("""{"jsonrpc":"2.0","error":{"code":-32601,"message":"Method not found"},"id":1}""");

            // Act
            var act = async () => await client.GetSlotAsync();

            // Assert
            (await act.Should().ThrowAsync<RpcException>()).Which.Code.Should().Be(-32601);
        }

        [Test]
        public async Task HttpError_ThrowsHttpRequestException()
        {
            // Arrange
            var client = Client("{}", HttpStatusCode.InternalServerError);

            // Act
            var act = async () => await client.GetSlotAsync();

            // Assert
            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
