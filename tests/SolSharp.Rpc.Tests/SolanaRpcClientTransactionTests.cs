using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

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
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":"Sig1111111111111111111111111111111111111111","id":1}""");
            byte[] transaction = [1, 2, 3, 4];

            // Act
            var signature = await client.SendTransactionAsync(transaction);

            // Assert
            signature.Should().Be("Sig1111111111111111111111111111111111111111");
            handler.CapturedRequestBody.Should().Contain("\"sendTransaction\"");
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
            handler.CapturedRequestBody.Should().Contain(Convert.ToBase64String(transaction));
            // Null options are dropped (WhenWritingNull), so unset fields never reach the node.
            handler.CapturedRequestBody.Should().NotContain("maxRetries");
            handler.CapturedRequestBody.Should().NotContain("minContextSlot");
        }

        [Test]
        public async Task SendsOptionsWhenProvided()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":"SigOpt111111111111111111111111111111111111","id":1}""");
            var options = new SendTransactionOptions
            {
                SkipPreflight = true,
                PreflightCommitment = Commitment.Processed,
                MaxRetries = 3,
                MinContextSlot = 42
            };

            // Act
            await client.SendTransactionAsync([1, 2, 3], options);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"skipPreflight\":true");
            handler.CapturedRequestBody.Should().Contain("\"preflightCommitment\":\"processed\"");
            handler.CapturedRequestBody.Should().Contain("\"maxRetries\":3");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":42");
        }
    }

    [TestFixture]
    public sealed class SimulateTransactionAsync
    {
        [Test]
        public async Task ParsesLogsAndUnitsFromContextValue()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"logs":["Program log: ok"],"unitsConsumed":1234}},"id":1}""");

            // Act
            var result = await client.SimulateTransactionAsync([1, 2, 3, 4]);

            // Assert
            result.IsError.Should().BeFalse();
            result.Logs.Should().ContainSingle().Which.Should().Be("Program log: ok");
            result.UnitsConsumed.Should().Be(1234);
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":{"InstructionError":[0,"Custom"]},"logs":[],"unitsConsumed":0}},"id":1}""");

            // Act
            var result = await client.SimulateTransactionAsync([9]);

            // Assert
            result.IsError.Should().BeTrue();
        }

        [Test]
        public async Task SendsOptionsWhenProvided()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"logs":[],"unitsConsumed":0}},"id":1}""");
            var options = new SimulateTransactionOptions
            {
                SigVerify = true,
                ReplaceRecentBlockhash = true,
                Commitment = Commitment.Processed,
                MinContextSlot = 7
            };

            // Act
            await client.SimulateTransactionAsync([1, 2, 3], options);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"sigVerify\":true");
            handler.CapturedRequestBody.Should().Contain("\"replaceRecentBlockhash\":true");
            handler.CapturedRequestBody.Should().Contain("\"commitment\":\"processed\"");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":7");
        }
    }
}
