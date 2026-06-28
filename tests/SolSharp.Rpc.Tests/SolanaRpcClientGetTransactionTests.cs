using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

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
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"slot":100,"blockTime":1700000000,"transaction":["AQID","base64"],"meta":{"err":null,"fee":5000,"preBalances":[100,200],"postBalances":[95,205],"logMessages":["Program log: ok"],"computeUnitsConsumed":1234},"version":0},"id":1}""");

            // Act
            var transaction = await client.GetTransactionAsync("Sig1111");

            // Assert
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

            byte[] expectedBytes = [1, 2, 3]; // "AQID" base64
            transaction.Transaction.Should().Equal(expectedBytes);

            handler.CapturedRequestBody.Should().Contain("\"getTransaction\"");
            handler.CapturedRequestBody.Should().Contain("Sig1111");
            handler.CapturedRequestBody.Should().Contain("maxSupportedTransactionVersion");
        }

        [Test]
        public async Task ReturnsNullWhenNotFound()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":null,"id":1}""");

            // Act
            var transaction = await client.GetTransactionAsync("Sig1111");

            // Assert
            transaction.Should().BeNull();
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"slot":7,"blockTime":null,"meta":{"err":{"InstructionError":[0,"Custom"]},"fee":5000}},"id":1}""");

            // Act
            var transaction = await client.GetTransactionAsync("Sig1111");

            // Assert
            transaction!.Meta!.IsError.Should().BeTrue();
            var error = transaction.Meta!.Error!;
            error.Kind.Should().Be("InstructionError");
            error.InstructionIndex.Should().Be(0);
        }

        [Test]
        public async Task ParsesTokenBalancesInnerInstructionsAndLoadedAddresses()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"slot":100,"transaction":["AQID","base64"],"meta":{"err":null,"fee":5000,"preTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"11111111111111111111111111111111","uiTokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"}}],"postTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"11111111111111111111111111111111","uiTokenAmount":{"amount":"2000000","decimals":6,"uiAmount":2.0,"uiAmountString":"2"}}],"innerInstructions":[{"index":0,"instructions":[{"programIdIndex":5,"accounts":[1,2,3],"data":"3Bxs","stackHeight":2}]}],"loadedAddresses":{"writable":["So11111111111111111111111111111111111111112"],"readonly":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"]}}},"id":1}""");

            // Act
            var meta = (await client.GetTransactionAsync("Sig1111"))!.Meta!;

            // Assert
            var pre = meta.PreTokenBalances.Should().ContainSingle().Subject;
            pre.AccountIndex.Should().Be(1);
            pre.Mint.Should().Be(PublicKey.Parse("EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"));
            pre.Owner.Should().Be(PublicKey.Parse("11111111111111111111111111111111"));
            pre.UiTokenAmount.Amount.Should().Be("1000000");
            pre.UiTokenAmount.Decimals.Should().Be(6);

            meta.PostTokenBalances.Should().ContainSingle().Which.UiTokenAmount.Amount.Should().Be("2000000");

            var inner = meta.InnerInstructions.Should().ContainSingle().Subject;
            inner.Index.Should().Be(0);
            var cpi = inner.Instructions.Should().ContainSingle().Subject;
            cpi.ProgramIdIndex.Should().Be(5);
            cpi.Accounts.Should().Equal(1, 2, 3);
            cpi.Data.Should().Be("3Bxs");
            cpi.StackHeight.Should().Be(2);

            meta.LoadedAddresses.Should().NotBeNull();
            meta.LoadedAddresses!.Writable.Should().ContainSingle()
                .Which.Should().Be(PublicKey.Parse("So11111111111111111111111111111111111111112"));
            meta.LoadedAddresses.Readonly.Should().ContainSingle()
                .Which.Should().Be(PublicKey.Parse("TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"));
        }
    }
}
