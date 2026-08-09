using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientSignaturesTests
{
    private const string Address = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetSignaturesForAddressAsync
    {
        [Test]
        public async Task ParsesEntriesAndRequestsTheAddress()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":[{"signature":"sig11","slot":100,"err":null,"memo":null,"blockTime":1700000000,"confirmationStatus":"finalized","transactionIndex":17}],"id":1}""");

            // Act
            var signatures = await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address));

            // Assert
            signatures.Should().ContainSingle();
            signatures[0].Signature.Should().Be("sig11");
            signatures[0].Slot.Should().Be(100);
            signatures[0].BlockTime.Should().Be(1700000000);
            signatures[0].ConfirmationStatus.Should().Be("finalized");
            signatures[0].TransactionIndex.Should().Be(17);
            signatures[0].IsError.Should().BeFalse();

            handler.CapturedRequestBody.Should().Contain("\"getSignaturesForAddress\"");
            handler.CapturedRequestBody.Should().Contain(Address);
            // Unset options are dropped (WhenWritingNull), so they never reach the node.
            handler.CapturedRequestBody.Should().NotContain("limit");
            handler.CapturedRequestBody.Should().NotContain("before");
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":[{"signature":"sigErr","slot":5,"err":{"InstructionError":[0,"Custom"]},"memo":null,"blockTime":null,"confirmationStatus":null}],"id":1}""");

            // Act
            var signatures = await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address));

            // Assert
            signatures[0].IsError.Should().BeTrue();
        }

        [Test]
        public async Task MissingMandatoryEntryFields_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":[{}],"id":1}""");

            // Act
            var act = async () => await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address));

            // Assert
            await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        }

        [Test]
        public async Task NullEntry_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":[null],"id":1}""");

            // Act
            var act = async () => await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address));

            // Assert
            await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        }

        [Test]
        public async Task SendsLimitAndBeforeWhenProvided()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":[],"id":1}""");
            var options = new GetSignaturesForAddressOptions { Limit = 5, Before = "cursorSig" };

            // Act
            var signatures = await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address), options);

            // Assert
            signatures.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Contain("\"limit\":5");
            handler.CapturedRequestBody.Should().Contain("\"before\":\"cursorSig\"");
        }

        [Test]
        public async Task SendsUntilWhenProvided()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":[],"id":1}""");
            var options = new GetSignaturesForAddressOptions { Until = "untilSig" };

            // Act
            await client.GetSignaturesForAddressAsync(PublicKey.Parse(Address), options);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"until\":\"untilSig\"");
        }
    }
}
