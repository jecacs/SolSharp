using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientConfirmTests
{
    private const string ConfirmedStatus =
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":5,"status":{"Ok":null},"err":null,"confirmationStatus":"confirmed"}]},"id":1}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    private static SolanaRpcClient Sequenced(params string[] responses)
    {
        var messages = responses
            .Select(static json => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
            .ToArray();
        var http = new HttpClient(new SequenceHandler(messages)) { BaseAddress = new("http://localhost") };
        return new(http);
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
            statuses[0]!.Status!.Value.GetProperty("Ok").ValueKind.Should().Be(JsonValueKind.Null);
            statuses[0]!.ConfirmationStatus.Should().Be("confirmed");
            statuses[0]!.IsError.Should().BeFalse();
            statuses[1].Should().BeNull();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getSignatureStatuses","params":[["Sig111","Sig222"],{"searchTransactionHistory":false}]}""");
        }

        [TestCase("{}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":null,\"err\":null,\"confirmationStatus\":\"confirmed\"}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":{},\"err\":null,\"confirmationStatus\":\"confirmed\"}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":{\"Ok\":null,\"extra\":1},\"err\":null,\"confirmationStatus\":\"confirmed\"}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":{\"Ok\":1},\"err\":null,\"confirmationStatus\":\"confirmed\"}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":{\"Err\":\"failure\"},\"err\":null,\"confirmationStatus\":\"confirmed\"}")]
        [TestCase("{\"slot\":10,\"confirmations\":5,\"status\":{\"Ok\":null},\"err\":null,\"confirmationStatus\":\"future\"}")]
        public async Task MalformedStatus_ThrowsJsonException(string status)
        {
            // Arrange
            var response = """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[__STATUS__]},"id":1}"""
                .Replace("__STATUS__", status, StringComparison.Ordinal);
            var (client, _) = Make(response);

            // Act
            var act = async () => await client.GetSignatureStatusesAsync(["Sig111"]);

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }
    }

    [TestFixture]
    public sealed class GetSignatureStatusesWithOptionsAsync
    {
        [Test]
        public async Task DefaultOptions_PreserveProcessedBankRequestAndNullStatuses()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[null]},"id":1}""");

            // Act
            var statuses = await client.GetSignatureStatusesWithOptionsAsync(["signature"], new());

            // Assert
            statuses.Should().ContainSingle().Which.Should().BeNull();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getSignatureStatuses","params":[["signature"],{"searchTransactionHistory":false}]}""");
        }

        [TestCase(Commitment.Processed, "processed")]
        [TestCase(Commitment.Confirmed, "confirmed")]
        [TestCase(Commitment.Finalized, "finalized")]
        public async Task ExplicitOptions_SendExactConfigAndParseStatuses(Commitment commitment, string wireCommitment)
        {
            // Arrange
            var (client, handler) = Make(ConfirmedStatus);
            var options = new GetSignatureStatusesOptions
            {
                SearchTransactionHistory = true,
                Commitment = commitment,
                MinContextSlot = 42
            };

            // Act
            var statuses = await client.GetSignatureStatusesWithOptionsAsync(["signature"], options);

            // Assert
            statuses.Should().ContainSingle().Which!.ConfirmationStatus.Should().Be("confirmed");
            handler.CapturedRequestBody.Should().Be(
                $$"""{"jsonrpc":"2.0","id":1,"method":"getSignatureStatuses","params":[["signature"],{"searchTransactionHistory":true,"commitment":"{{wireCommitment}}","minContextSlot":42}]}""");
        }

        [TestCase(0ul)]
        [TestCase(ulong.MaxValue)]
        public async Task MinContextSlot_IsSentWithoutTruncationOrImplicitCommitment(ulong minContextSlot)
        {
            // Arrange
            var (client, handler) = Make(ConfirmedStatus);

            // Act
            await client.GetSignatureStatusesWithOptionsAsync(["signature"], new() { MinContextSlot = minContextSlot });

            // Assert
            using var request = JsonDocument.Parse(handler.CapturedRequestBody!);
            var config = request.RootElement.GetProperty("params")[1];
            config.GetProperty("minContextSlot").GetUInt64().Should().Be(minContextSlot);
            config.TryGetProperty("commitment", out _).Should().BeFalse();
        }

        [Test]
        public async Task MinContextSlotNotReached_PreservesNodeError()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","error":{"code":-32016,"message":"Minimum context slot has not been reached","data":{"contextSlot":41}},"id":1}""");

            // Act
            var act = () => client.GetSignatureStatusesWithOptionsAsync(["signature"], new() { MinContextSlot = 42 });

            // Assert
            var error = (await act.Should().ThrowAsync<RpcException>()).Which;
            error.Code.Should().Be(-32016);
            error.ErrorData!.Value.GetProperty("contextSlot").GetUInt64().Should().Be(41);
        }

        [Test]
        public async Task MissingContextValue_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1}},"id":1}""");

            // Act
            var act = () => client.GetSignatureStatusesWithOptionsAsync(["signature"], new());

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }

        [Test]
        public async Task NullSignatures_ThrowsBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make(ConfirmedStatus);

            // Act
            var act = () => client.GetSignatureStatusesWithOptionsAsync(null!, new());

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("signatures");
            handler.CapturedRequestBody.Should().BeNull();
        }

        [Test]
        public async Task NullOptions_ThrowsBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make(ConfirmedStatus);

            // Act
            var act = () => client.GetSignatureStatusesWithOptionsAsync(["signature"], null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
            handler.CapturedRequestBody.Should().BeNull();
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
        public async Task MalformedEmptyStatus_CannotBeMistakenForFinalized()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{}]},"id":1}""");

            // Act
            var act = async () => await client.ConfirmTransactionAsync("Sig111", Commitment.Finalized);

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }

        [Test]
        public async Task Timeout_CancelsInFlightStatusRequest()
        {
            // Arrange
            var handler = new BlockingHandler();
            var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
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
            var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
            var client = new SolanaRpcClient(http);

            var status = await client.ConfirmTransactionAsync("Sig111");

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
            """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":__CONFIRMATIONS__,"status":{"Ok":null},"err":null}]} ,"id":1}"""
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
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":[{"slot":10,"confirmations":5,"status":{"Err":{"InstructionError":[0,"Custom"]}},"err":{"InstructionError":[0,"Custom"]},"confirmationStatus":"confirmed"}]},"id":1}""");

            // Act
            Func<Task> act = () => client.SendAndConfirmTransactionAsync([1, 2, 3]);

            // Assert
            await act.Should().ThrowAsync<TransactionFailedException>();
        }
    }

    [TestFixture]
    public sealed class TransactionFailedExceptionConstructor
    {
        [Test]
        public void Error_RemainsUsableAfterSourceDocumentIsDisposed()
        {
            // Arrange
            TransactionFailedException exception;
            using (var document = JsonDocument.Parse("""{"InstructionError":[0,"Custom"]}"""))
                exception = new("Sig111", document.RootElement);

            // Act
            var error = exception.Error;

            // Assert
            error.Should().NotBeNull();
            error.Value.GetProperty("InstructionError")[1].GetString().Should().Be("Custom");
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
