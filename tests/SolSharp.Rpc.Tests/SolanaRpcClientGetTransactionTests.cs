using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;

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
                """{"jsonrpc":"2.0","result":{"slot":100,"blockTime":1700000000,"transaction":["AQID","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":5000,"preBalances":[100,200],"postBalances":[95,205],"logMessages":["Program log: ok"],"computeUnitsConsumed":1234},"version":0},"id":1}""");

            // Act
            // Keep the default literal in the legacy third-argument position as a source-compatibility KAT.
            var transaction = await client.GetTransactionAsync("Sig1111", Commitment.Confirmed, default);

            // Assert
            transaction.Should().NotBeNull();
            transaction!.Slot.Should().Be(100);
            transaction.BlockTime.Should().Be(1700000000);
            transaction.Version.Should().Be(RpcTransactionVersion.FromNumber(0));
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
            handler.CapturedRequestBody.Should().Contain("\"maxSupportedTransactionVersion\":0");
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
        public async Task ExplicitVersionOptIn_SendsVersionOneAndPreservesOpaqueBytes()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"slot":101,"blockTime":null,"transaction":["gQECAw==","base64"],"meta":null,"version":1},"id":1}""");

            // Act
            var transaction = await client.GetTransactionWithMaxVersionAsync(
                "SigV1", maxSupportedTransactionVersion: 1);

            // Assert
            transaction!.Version.Should().Be(RpcTransactionVersion.FromNumber(1));
            transaction.Transaction.Should().Equal(129, 1, 2, 3);
            handler.CapturedRequestBody.Should().Contain("\"maxSupportedTransactionVersion\":1");
        }

        [Test]
        public async Task SurfacesErrAsIsError()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"slot":7,"blockTime":null,"transaction":["","base64"],"meta":{"err":{"InstructionError":[0,"Custom"]},"status":{"Err":{"InstructionError":[0,"Custom"]}},"fee":5000,"preBalances":[],"postBalances":[]}},"id":1}""");

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
                """{"jsonrpc":"2.0","result":{"slot":100,"blockTime":null,"transaction":["AQID","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":5000,"preBalances":[],"postBalances":[],"preTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"11111111111111111111111111111111","uiTokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"}}],"postTokenBalances":[{"accountIndex":1,"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"11111111111111111111111111111111","uiTokenAmount":{"amount":"2000000","decimals":6,"uiAmount":2.0,"uiAmountString":"2"}}],"innerInstructions":[{"index":0,"instructions":[{"programIdIndex":5,"accounts":[1,2,3],"data":"3Bxs","stackHeight":2}]}],"loadedAddresses":{"writable":["So11111111111111111111111111111111111111112"],"readonly":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"]}}},"id":1}""");

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

        [Test]
        public async Task UnexpectedTransactionEncoding_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make(
                "{\"jsonrpc\":\"2.0\",\"result\":{\"slot\":100,\"blockTime\":null,\"transaction\":[\"AQID\",\"base58\"],\"meta\":null},\"id\":1}");

            // Act
            var act = async () => await client.GetTransactionAsync("Sig1111");

            // Assert
            await act.Should().ThrowAsync<JsonException>()
                .WithMessage("*base64*");
        }

        [Test]
        public async Task IncompleteTransactionTuple_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make(
                "{\"jsonrpc\":\"2.0\",\"result\":{\"slot\":100,\"blockTime\":null,\"transaction\":[\"AQID\"],\"meta\":null},\"id\":1}");

            // Act
            var act = async () => await client.GetTransactionAsync("Sig1111");

            // Assert
            await act.Should().ThrowAsync<JsonException>()
                .WithMessage("*two-element array*");
        }

        [Test]
        public async Task ParsesCurrentMetadataAndLegacyVersion()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"slot":100,"blockTime":1700000000,"transactionIndex":4,"transaction":["AQID","base64"],"meta":{"err":null,"status":{"Ok":null},"fee":5000,"preBalances":[],"postBalances":[],"costUnits":77,"returnData":{"programId":"11111111111111111111111111111111","data":["BAU=","base64"]},"rewards":[{"pubkey":"11111111111111111111111111111111","lamports":-5,"postBalance":95,"rewardType":"Fee","commission":7,"commissionBps":725}]},"version":"legacy"},"id":1}""");

            // Act
            var transaction = await client.GetTransactionAsync("Sig1111");

            // Assert
            transaction!.TransactionIndex.Should().Be(4);
            transaction.Version.Should().Be(RpcTransactionVersion.Legacy);
            transaction.Meta!.Status.GetProperty("Ok").ValueKind.Should().Be(JsonValueKind.Null);
            transaction.Meta.CostUnits.Should().Be(77);
            transaction.Meta.ReturnData!.Data.Should().Equal(4, 5);
            var reward = transaction.Meta.Rewards.Should().ContainSingle().Subject;
            reward.PublicKey.Should().Be(PublicKey.Parse("11111111111111111111111111111111"));
            reward.Lamports.Should().Be(-5);
            reward.PostBalance.Should().Be(95);
            reward.RewardType.Should().Be("Fee");
            reward.Commission.Should().Be(7);
            reward.CommissionBps.Should().Be(725);
        }
    }
}
