using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class RpcBatchTests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class ExecuteAsync
    {
        [Test]
        public async Task ResolvesEveryCall_MatchingOutOfOrderResponsesById()
        {
            // Arrange: the node replies out of order - ids must drive the matching.
            var (client, handler) = Make(
                """
                [
                  {"jsonrpc":"2.0","result":348543210,"id":2},
                  {"jsonrpc":"2.0","result":{"context":{"slot":1},"value":1000000},"id":1},
                  {"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"blockhash":"EkSnNWid2cvwEVnVx9aBqawnmiCNiDgp3gUdkDPTKN1N","lastValidBlockHeight":3090}},"id":3}
                ]
                """);

            var batch = client.CreateBatch();
            var balance = batch.GetBalanceAsync(Pk(2));
            var slot = batch.GetSlotAsync();
            var blockhash = batch.GetLatestBlockhashAsync();

            // Act
            await batch.ExecuteAsync();

            // Assert
            (await balance).Should().Be(1_000_000ul);
            (await slot).Should().Be(348543210ul);
            (await blockhash).Blockhash.Should().Be("EkSnNWid2cvwEVnVx9aBqawnmiCNiDgp3gUdkDPTKN1N");

            handler.CapturedRequestBody.Should().StartWith("[");
            handler.CapturedRequestBody.Should().Contain("\"id\":1").And.Contain("\"id\":2").And.Contain("\"id\":3");
            handler.CapturedRequestBody.Should().Contain("\"getBalance\"").And.Contain("\"getSlot\"").And.Contain("\"getLatestBlockhash\"");
        }

        [Test]
        public async Task PerCallError_FaultsOnlyThatTask()
        {
            // Arrange
            var (client, _) = Make(
                """
                [
                  {"jsonrpc":"2.0","result":{"context":{"slot":1},"value":5},"id":1},
                  {"jsonrpc":"2.0","error":{"code":-32602,"message":"Invalid params"},"id":2}
                ]
                """);

            var batch = client.CreateBatch();
            var good = batch.GetBalanceAsync(Pk(2));
            var bad = batch.GetBalanceAsync(Pk(3));

            // Act
            await batch.ExecuteAsync();

            // Assert
            (await good).Should().Be(5ul);
            var act = async () => await bad;
            (await act.Should().ThrowAsync<RpcException>()).Which.Code.Should().Be(-32602);
        }

        [Test]
        public async Task PerCallError_PreservesStructuredErrorData()
        {
            // Arrange
            var (client, _) = Make(
                """[{"jsonrpc":"2.0","error":{"code":-32002,"message":"Simulation failed","data":{"unitsConsumed":99}},"id":1}]""");
            var batch = client.CreateBatch();
            var call = batch.GetSlotAsync();

            // Act
            await batch.ExecuteAsync();

            // Assert
            var act = async () => await call;
            var exception = (await act.Should().ThrowAsync<RpcException>()).Which;
            exception.ErrorData!.Value.GetProperty("unitsConsumed").GetInt32().Should().Be(99);
        }

        [Test]
        public async Task MissingResponseEntry_FaultsThatTask()
        {
            // Arrange: the node answers only the first call.
            var (client, _) = Make("""[{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":5},"id":1}]""");

            var batch = client.CreateBatch();
            var answered = batch.GetBalanceAsync(Pk(2));
            var unanswered = batch.GetSlotAsync();

            // Act
            await batch.ExecuteAsync();

            // Assert
            (await answered).Should().Be(5ul);
            var act = async () => await unanswered;
            await act.Should().ThrowAsync<RpcException>();
        }

        [Test]
        public async Task NonArrayResponse_ThrowsAndFaultsAllTasks()
        {
            // Arrange: a provider with batching disabled replies with a single object.
            var (client, _) = Make("""{"jsonrpc":"2.0","error":{"code":-32600,"message":"batch disabled"},"id":1}""");

            var batch = client.CreateBatch();
            var call = batch.GetSlotAsync();

            // Act
            var act = () => batch.ExecuteAsync();

            // Assert
            await act.Should().ThrowAsync<RpcException>();
            var callAct = async () => await call;
            await callAct.Should().ThrowAsync<RpcException>();
        }

        [TestCase("[null]")]
        [TestCase("[1]")]
        [TestCase("[[]]")]
        [TestCase("[{\"jsonrpc\":\"1.0\",\"result\":1,\"id\":1}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"result\":1,\"error\":{\"code\":-1,\"message\":\"bad\"},\"id\":1}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"result\":1,\"id\":2}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"result\":1,\"id\":\"1\"}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"error\":\"bad\",\"id\":1}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"error\":{},\"id\":1}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"error\":{\"code\":\"-1\",\"message\":\"bad\"},\"id\":1}]")]
        [TestCase("[{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-1,\"message\":7},\"id\":1}]")]
        public async Task MalformedResponse_ThrowsAndTerminatesEveryQueuedTask(string response)
        {
            // Arrange
            var (client, _) = Make(response);
            var batch = client.CreateBatch();
            var call = batch.GetSlotAsync();

            // Act
            var act = () => batch.ExecuteAsync();

            // Assert
            await act.Should().ThrowAsync<RpcException>();
            call.IsCompleted.Should().BeTrue();
            var callAct = async () => await call;
            await callAct.Should().ThrowAsync<RpcException>();
        }

        [Test]
        public async Task DuplicateResponseId_ThrowsAndTerminatesEveryQueuedTask()
        {
            // Arrange
            var (client, _) = Make(
                """
                [
                  {"jsonrpc":"2.0","result":1,"id":1},
                  {"jsonrpc":"2.0","result":2,"id":1}
                ]
                """);
            var batch = client.CreateBatch();
            var first = batch.GetSlotAsync();
            var second = batch.GetSlotAsync();

            // Act
            var act = () => batch.ExecuteAsync();

            // Assert
            await act.Should().ThrowAsync<RpcException>();
            first.IsCompleted.Should().BeTrue();
            second.IsCompleted.Should().BeTrue();
        }

        [Test]
        public async Task SendTransaction_IsBatchable()
        {
            // Arrange
            var (client, handler) = Make("""[{"jsonrpc":"2.0","result":"SigBatch11111111111111111111111111111111111","id":1}]""");
            byte[] transaction = [1, 2, 3];

            var batch = client.CreateBatch();
            var signature = batch.SendTransactionAsync(transaction);

            // Act
            await batch.ExecuteAsync();

            // Assert
            (await signature).Should().Be("SigBatch11111111111111111111111111111111111");
            handler.CapturedRequestBody.Should().Contain("\"sendTransaction\"");
            handler.CapturedRequestBody.Should().Contain(Convert.ToBase64String(transaction));
        }

        [Test]
        public async Task Empty_Throws()
        {
            // Arrange
            var (client, _) = Make("[]");

            // Act & Assert
            var act = () => client.CreateBatch().ExecuteAsync();
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Test]
        public async Task ExecutedTwice_Throws()
        {
            // Arrange
            var (client, _) = Make("""[{"jsonrpc":"2.0","result":1,"id":1}]""");
            var batch = client.CreateBatch();
            _ = batch.GetSlotAsync();
            await batch.ExecuteAsync();

            // Act & Assert
            var act = () => batch.ExecuteAsync();
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
