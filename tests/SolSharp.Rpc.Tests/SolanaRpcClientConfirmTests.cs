using System.Net;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientConfirmTests
{
    private const string ConfirmedStatus =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":5,"err":null,"confirmationStatus":"confirmed"}]},"id":1}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static SolanaRpcClient Sequenced(params string[] responses)
    {
        var messages = responses
            .Select(json => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
            .ToArray();
        var http = new HttpClient(new SequenceHandler(messages)) { BaseAddress = new Uri("http://localhost") };
        return new SolanaRpcClient(http);
    }

    [TestFixture]
    public sealed class GetSignatureStatusesAsync
    {
        [Test]
        public async Task ParsesStatusesAndPreservesNulls()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":5,"err":null,"confirmationStatus":"confirmed"},null]},"id":1}""");

            // Act
            var statuses = await client.GetSignatureStatusesAsync(["Sig111", "Sig222"]);

            // Assert
            statuses.Should().HaveCount(2);
            statuses[0]!.ConfirmationStatus.Should().Be("confirmed");
            statuses[0]!.IsError.Should().BeFalse();
            statuses[1].Should().BeNull();
            handler.CapturedRequestBody.Should().Contain("\"getSignatureStatuses\"");
        }
    }

    [TestFixture]
    public sealed class ConfirmTransactionAsync
    {
        [Test]
        public async Task ReturnsOnceCommitmentReached()
        {
            // Arrange
            var (client, _) = Make(ConfirmedStatus);

            // Act
            var status = await client.ConfirmTransactionAsync("Sig111");

            // Assert
            status.ConfirmationStatus.Should().Be("confirmed");
            status.IsError.Should().BeFalse();
        }

        [Test]
        public async Task ThrowsTimeoutWhenUnconfirmed()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[null]},"id":1}""");

            // Act
            Func<Task> act = () => client.ConfirmTransactionAsync("Sig111", timeout: TimeSpan.Zero);

            // Assert
            await act.Should().ThrowAsync<TimeoutException>();
        }
    }

    [TestFixture]
    public sealed class SendAndConfirmTransactionAsync
    {
        [Test]
        public async Task SendsThenConfirms_ReturnsSignature()
        {
            // Arrange
            var client = Sequenced("""{"jsonrpc":"2.0","result":"Sig111","id":1}""", ConfirmedStatus);
            byte[] transaction = [1, 2, 3];

            // Act
            var signature = await client.SendAndConfirmTransactionAsync(transaction);

            // Assert
            signature.Should().Be("Sig111");
        }

        [Test]
        public async Task ThrowsWhenTransactionFailsOnChain()
        {
            // Arrange
            var client = Sequenced(
                """{"jsonrpc":"2.0","result":"SigFail","id":1}""",
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"err":{"InstructionError":[0,"Custom"]},"confirmationStatus":"confirmed"}]},"id":1}""");

            // Act
            Func<Task> act = () => client.SendAndConfirmTransactionAsync([1, 2, 3]);

            // Assert
            await act.Should().ThrowAsync<TransactionFailedException>();
        }
    }
}
