using System.Net;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
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
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":5,"status":{"Ok":null},"err":null,"confirmationStatus":"confirmed"},null]},"id":1}""");

            // Act
            var statuses = await client.GetSignatureStatusesAsync(["Sig111", "Sig222"]);

            // Assert
            statuses.Should().HaveCount(2);
            statuses[0]!.Slot.Should().Be(10ul);
            statuses[0]!.Confirmations.Should().Be(5);
            statuses[0]!.Status!.Value.GetProperty("Ok").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
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

        [Test]
        public async Task Timeout_CancelsInFlightStatusRequest()
        {
            // Arrange
            var handler = new BlockingHandler();
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(http);

            // Act
            Func<Task> act = () => client.ConfirmTransactionAsync("Sig111", timeout: TimeSpan.FromMilliseconds(50));

            // Assert
            await act.Should().ThrowAsync<TimeoutException>();
            await handler.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        [Test]
        public async Task TimeoutBeyondTimerLimit_IsAccepted()
        {
            // Arrange
            var (client, _) = Make(ConfirmedStatus);

            // Act
            var status = await client.ConfirmTransactionAsync("Sig111", timeout: TimeSpan.FromDays(100));

            // Assert
            status.ConfirmationStatus.Should().Be("confirmed");
        }

        [Test]
        public async Task MissingConfirmationStatus_UsesUpstreamConfirmationCountThreshold()
        {
            var handler = new SequenceHandler(
                Json(StatusWithoutConfirmationStatus("1")),
                Json(StatusWithoutConfirmationStatus("2")));
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            var client = new SolanaRpcClient(http);

            var status = await client.ConfirmTransactionAsync("Sig111", Commitment.Confirmed);

            status.Confirmations.Should().Be(2);
            handler.CallCount.Should().Be(2, "one confirmation is still processed in the legacy response shape");
        }

        [Test]
        public async Task MissingConfirmationStatus_NullConfirmationsMeansFinalized()
        {
            var (client, _) = Make(StatusWithoutConfirmationStatus("null"));

            var status = await client.ConfirmTransactionAsync("Sig111", Commitment.Finalized);

            status.Confirmations.Should().BeNull();
        }

        [Test]
        public async Task MissingConfirmationStatus_ZeroConfirmationsMeansProcessed()
        {
            var (client, _) = Make(StatusWithoutConfirmationStatus("0"));

            var status = await client.ConfirmTransactionAsync("Sig111", Commitment.Processed);

            status.Confirmations.Should().Be(0);
        }

        private static string StatusWithoutConfirmationStatus(string confirmations) =>
            """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":__CONFIRMATIONS__,"err":null,"confirmationStatus":null}]} ,"id":1}"""
                .Replace("__CONFIRMATIONS__", confirmations);

        private static HttpResponseMessage Json(string body)
            => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
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

    private sealed class BlockingHandler : HttpMessageHandler
    {
        public TaskCompletionSource CancellationObserved { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The blocking handler unexpectedly resumed.");
            }
            catch (OperationCanceledException)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
        }
    }
}
