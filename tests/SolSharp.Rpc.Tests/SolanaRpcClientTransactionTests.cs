using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientTransactionTests
{
    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class SendTransactionAsync
    {
        [Test]
        public async Task ReturnsSignatureAndBase64EncodesTheTransaction()
        {
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":"Sig1111111111111111111111111111111111111111","id":1}""");
            byte[] transaction = [1, 2, 3, 4];

            var signature = await client.SendTransactionAsync(transaction);

            signature.Should().Be("Sig1111111111111111111111111111111111111111");
            handler.CapturedRequestBody.Should().Contain("\"sendTransaction\"");
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
            handler.CapturedRequestBody.Should().Contain(Convert.ToBase64String(transaction));
            // Null options are dropped (WhenWritingNull), so unset fields never reach the node.
            handler.CapturedRequestBody.Should().NotContain("maxRetries");
            handler.CapturedRequestBody.Should().NotContain("minContextSlot");
        }
    }

    [TestFixture]
    public sealed class SimulateTransactionAsync
    {
        [Test]
        public async Task ParsesLogsAndUnitsFromContextValue()
        {
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"logs":["Program log: ok"],"unitsConsumed":1234}},"id":1}""");

            var result = await client.SimulateTransactionAsync([1, 2, 3, 4]);

            result.IsError.Should().BeFalse();
            result.Logs.Should().ContainSingle().Which.Should().Be("Program log: ok");
            result.UnitsConsumed.Should().Be(1234);
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":{"InstructionError":[0,"Custom"]},"logs":[],"unitsConsumed":0}},"id":1}""");

            var result = await client.SimulateTransactionAsync([9]);

            result.IsError.Should().BeTrue();
        }
    }
}
