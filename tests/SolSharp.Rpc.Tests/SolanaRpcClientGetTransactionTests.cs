using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientGetTransactionTests
{
    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetTransactionAsync
    {
        [Test]
        public async Task ParsesSlotBlockTimeAndMeta()
        {
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"slot":100,"blockTime":1700000000,"transaction":["AQID","base64"],"meta":{"err":null,"fee":5000,"preBalances":[100,200],"postBalances":[95,205],"logMessages":["Program log: ok"],"computeUnitsConsumed":1234},"version":0},"id":1}""");

            var transaction = await client.GetTransactionAsync("Sig1111");

            transaction.Should().NotBeNull();
            transaction!.Slot.Should().Be(100);
            transaction.BlockTime.Should().Be(1700000000);
            transaction.Meta.Should().NotBeNull();
            transaction.Meta!.IsError.Should().BeFalse();
            transaction.Meta.Fee.Should().Be(5000);
            transaction.Meta.ComputeUnitsConsumed.Should().Be(1234);
            transaction.Meta.PreBalances.Should().Equal(100ul, 200ul);
            transaction.Meta.PostBalances.Should().Equal(95ul, 205ul);
            transaction.Meta.LogMessages.Should().ContainSingle().Which.Should().Be("Program log: ok");

            handler.CapturedRequestBody.Should().Contain("\"getTransaction\"");
            handler.CapturedRequestBody.Should().Contain("Sig1111");
            handler.CapturedRequestBody.Should().Contain("maxSupportedTransactionVersion");
        }

        [Test]
        public async Task ReturnsNullWhenNotFound()
        {
            var (client, _) = Make("""{"jsonrpc":"2.0","result":null,"id":1}""");

            var transaction = await client.GetTransactionAsync("Sig1111");

            transaction.Should().BeNull();
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"slot":7,"blockTime":null,"meta":{"err":{"InstructionError":[0,"Custom"]},"fee":5000}},"id":1}""");

            var transaction = await client.GetTransactionAsync("Sig1111");

            transaction!.Meta!.IsError.Should().BeTrue();
        }
    }
}
