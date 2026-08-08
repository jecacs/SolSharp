using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
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
        public async Task DefaultsPreflightCommitmentToConfirmed()
        {
            // Arrange: the node's own preflight default is finalized, where a blockhash fetched at the
            // client's confirmed default may not exist yet - the pairing must be coherent out of the box.
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":"Sig2222222222222222222222222222222222222222","id":1}""");

            // Act
            await client.SendTransactionAsync([1, 2, 3]);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"preflightCommitment\":\"confirmed\"");
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
        public async Task DefaultsCommitmentToConfirmed()
        {
            // Arrange
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"logs":[],"unitsConsumed":0}},"id":1}""");

            // Act
            await client.SimulateTransactionAsync([1, 2, 3]);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"commitment\":\"confirmed\"");
            handler.CapturedRequestBody.Should().NotContain("\"innerInstructions\"");
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
                Commitment = Commitment.Processed,
                MinContextSlot = 7
            };

            // Act
            await client.SimulateTransactionAsync([1, 2, 3], options);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"sigVerify\":true");
            handler.CapturedRequestBody.Should().Contain("\"replaceRecentBlockhash\":false");
            handler.CapturedRequestBody.Should().Contain("\"commitment\":\"processed\"");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":7");
        }

        [Test]
        public async Task SendsAccountAndInnerInstructionOptions_AndParsesFullCurrentResult()
        {
            // Arrange
            var system = PublicKey.Parse(SolanaProgramIds.SystemProgram);
            var token = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":88,"apiVersion":"3.1.7"},"value":{"err":null,"logs":["ok"],"accounts":[{"lamports":9,"data":["AQID","base64"],"owner":"11111111111111111111111111111111","executable":false,"rentEpoch":18446744073709551615,"space":3}],"unitsConsumed":1234,"loadedAccountsDataSize":456,"returnData":{"programId":"11111111111111111111111111111111","data":["BAU=","base64"]},"innerInstructions":[{"index":0,"instructions":[{"program":"system","programId":"11111111111111111111111111111111","parsed":{"type":"transfer","info":{"lamports":1}},"stackHeight":2}]}],"replacementBlockhash":{"blockhash":"CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8","lastValidBlockHeight":999},"fee":5000,"preBalances":[10,20],"postBalances":[5,20],"preTokenBalances":[{"accountIndex":1,"mint":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","uiTokenAmount":{"amount":"10","decimals":1,"uiAmount":1.0,"uiAmountString":"1"}}],"postTokenBalances":[],"loadedAddresses":{"writable":["11111111111111111111111111111111"],"readonly":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"]}}},"id":1}""");
            var options = new SimulateTransactionOptions
            {
                Accounts = [system, token],
                InnerInstructions = true,
                ReplaceRecentBlockhash = true
            };

            // Act
            var result = await client.SimulateTransactionAsync([1, 2, 3], options);

            // Assert
            using var request = JsonDocument.Parse(handler.CapturedRequestBody!);
            var config = request.RootElement.GetProperty("params")[1];
            config.GetProperty("innerInstructions").GetBoolean().Should().BeTrue();
            config.GetProperty("accounts").GetProperty("encoding").GetString().Should().Be("base64");
            config.GetProperty("accounts").GetProperty("addresses").EnumerateArray()
                .Select(static address => address.GetString()).Should().Equal(system.ToString(), token.ToString());

            result.Accounts.Should().ContainSingle();
            result.Accounts![0]!.Data.Should().Equal(1, 2, 3);
            result.Accounts[0]!.Space.Should().Be(3);
            result.LoadedAccountsDataSize.Should().Be(456);
            result.ReturnData!.ProgramId.Should().Be(system);
            result.ReturnData.Data.Should().Equal(4, 5);
            var inner = result.InnerInstructions.Should().ContainSingle().Subject;
            inner.Instructions.Should().ContainSingle().Which.Parsed!.Type.Should().Be("transfer");
            result.ReplacementBlockhash!.LastValidBlockHeight.Should().Be(999);
            result.Fee.Should().Be(5000);
            result.PreBalances.Should().Equal(10ul, 20ul);
            result.PostBalances.Should().Equal(5ul, 20ul);
            result.PreTokenBalances.Should().ContainSingle().Which.UiTokenAmount.Amount.Should().Be("10");
            result.PostTokenBalances.Should().BeEmpty();
            result.LoadedAddresses!.Writable.Should().ContainSingle().Which.Should().Be(system);
            result.LoadedAddresses.Readonly.Should().ContainSingle().Which.Should().Be(token);
        }

        [Test]
        public async Task MalformedReturnDataEncoding_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"returnData":{"programId":"11111111111111111111111111111111","data":["AQID","base58"]}}},"id":1}""");

            // Act
            var act = async () => await client.SimulateTransactionAsync([1]);

            // Assert
            await act.Should().ThrowAsync<JsonException>().WithMessage("*base64*");
        }

        [Test]
        public async Task MalformedReturnDataBytes_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"returnData":{"programId":"11111111111111111111111111111111","data":["%%%","base64"]}}},"id":1}""");

            // Act
            var act = async () => await client.SimulateTransactionAsync([1]);

            // Assert
            await act.Should().ThrowAsync<JsonException>().WithMessage("*Binary data*base64*");
        }

        [Test]
        public async Task SignatureVerificationAndBlockhashReplacement_ThrowsBeforeSending()
        {
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"err":null,"logs":[],"unitsConsumed":0}},"id":1}""");
            var options = new SimulateTransactionOptions
            {
                SigVerify = true,
                ReplaceRecentBlockhash = true
            };

            Func<Task> act = async () => await client.SimulateTransactionAsync([1, 2, 3], options);

            await act.Should().ThrowAsync<ArgumentException>().WithParameterName("options");
            handler.CapturedRequestBody.Should().BeNull();
        }
    }
}
