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
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":328},"value":{"blockhash":"EkSnNWid2cvwEVnVx9aBqawnmiCNiDgp3gUdkDPTKN1N","lastValidBlockHeight":3090}},"id":1}""");

            var result = await client.GetLatestBlockhashAsync();

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
            var client = Client("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":1000000000},"id":1}""");

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
            var client = Client("""{"jsonrpc":"2.0","result":348543210,"id":1}""");

            (await client.GetSlotAsync()).Should().Be(348543210);
        }
    }

    [TestFixture]
    public sealed class GetHealth
    {
        [Test]
        public async Task Ok_ReturnsTrue()
        {
            var client = Client("""{"jsonrpc":"2.0","result":"ok","id":1}""");

            (await client.GetHealthAsync()).Should().BeTrue();
        }

        [Test]
        public async Task NonOk_ReturnsFalse()
        {
            var client = Client("""{"jsonrpc":"2.0","result":"behind","id":1}""");

            (await client.GetHealthAsync()).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class GetVersion
    {
        [Test]
        public async Task ParsesSolanaCore()
        {
            var client = Client("""{"jsonrpc":"2.0","result":{"solana-core":"2.0.14","feature-set":3241752847},"id":1}""");

            (await client.GetVersionAsync()).SolanaCore.Should().Be("2.0.14");
        }
    }

    [TestFixture]
    public sealed class GetBlockHeight
    {
        [Test]
        public async Task ParsesBareValue()
        {
            var client = Client("""{"jsonrpc":"2.0","result":348543200,"id":1}""");

            (await client.GetBlockHeightAsync()).Should().Be(348543200);
        }
    }

    [TestFixture]
    public sealed class GetTokenSupply
    {
        [Test]
        public async Task ParsesAmountAndDecimals()
        {
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"amount":"1000000000","decimals":6,"uiAmount":1000.0,"uiAmountString":"1000"}},"id":1}""");

            var supply = await client.GetTokenSupplyAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));

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
            var client = Client("""{"jsonrpc":"2.0","result":2039280,"id":1}""");

            (await client.GetMinimumBalanceForRentExemptionAsync(165)).Should().Be(2039280);
        }
    }

    [TestFixture]
    public sealed class GetTransactionCount
    {
        [Test]
        public async Task ParsesBareValue()
        {
            var client = Client("""{"jsonrpc":"2.0","result":268,"id":1}""");

            (await client.GetTransactionCountAsync()).Should().Be(268);
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountBalance
    {
        [Test]
        public async Task ParsesAmountAndDecimals()
        {
            var client = Client(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"amount":"9864","decimals":2,"uiAmount":98.64,"uiAmountString":"98.64"}},"id":1}""");

            var balance = await client.GetTokenAccountBalanceAsync(PublicKey.Parse(SolanaProgramIds.TokenProgram));

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
            var client = Client("""{"jsonrpc":"2.0","error":{"code":-32601,"message":"Method not found"},"id":1}""");

            var act = async () => await client.GetSlotAsync();

            (await act.Should().ThrowAsync<RpcException>()).Which.Code.Should().Be(-32601);
        }

        [Test]
        public async Task HttpError_ThrowsHttpRequestException()
        {
            var client = Client("{}", HttpStatusCode.InternalServerError);

            var act = async () => await client.GetSlotAsync();

            await act.Should().ThrowAsync<HttpRequestException>();
        }
    }
}
