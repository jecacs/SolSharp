using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientDirectCoverageTests
{
    private const string Address = "11111111111111111111111111111111";
    private const string Program = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    private const string ProgramAccountJson =
        """{"pubkey":"11111111111111111111111111111111","account":{"data":["AQID","base64"],"executable":false,"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":165}}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string resultJson)
    {
        var handler = new FakeHttpMessageHandler(
            $$"""{"jsonrpc":"2.0","result":{{resultJson}},"id":1}""");
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    [TestFixture]
    public sealed class GetTokenAccountsByOwnerWithContextAsync
    {
        [Test]
        public async Task ProgramFilterAndOptions_ParseContextAndUsePinnedWireShape()
        {
            // Arrange
            var (client, handler) = Make(
                $$"""{"context":{"slot":51,"apiVersion":"2.0.0"},"value":[{{ProgramAccountJson}}]}""");
            var options = new GetAccountInfoOptions
            {
                Commitment = Commitment.Finalized,
                DataSlice = new(4, 8),
                MinContextSlot = 42
            };

            // Act
            var result = await client.GetTokenAccountsByOwnerWithContextAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByProgramId(PublicKey.Parse(Program)),
                options);

            // Assert
            result.Context.Should().NotBeNull();
            result.Context.Slot.Should().Be(51);
            result.Context.ApiVersion.Should().Be("2.0.0");
            result.Value.Should().ContainSingle();
            result.Value![0].PublicKey.Should().Be(PublicKey.Parse(Address));
            result.Value[0].Account.Data.Should().Equal(1, 2, 3);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByOwner","params":["11111111111111111111111111111111",{"programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"},{"encoding":"base64","commitment":"finalized","dataSlice":{"offset":4,"length":8},"minContextSlot":42}]}""");
        }

        [Test]
        public async Task NullFilter_IsRejectedBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make("null");

            // Act
            Func<Task> act = async () => await client.GetTokenAccountsByOwnerWithContextAsync(
                PublicKey.Parse(Address), null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("filter");
            handler.CapturedRequestBody.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetParsedAccountInfoWithOptionsAsync
    {
        [Test]
        public async Task ContextOptions_ParseValueAndUsePinnedWireShape()
        {
            // Arrange
            var (client, handler) = Make(
                """{"context":{"slot":52},"value":{"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":18446744073709551615,"space":165,"data":{"program":"spl-token","parsed":{"type":"account","info":{"mint":"11111111111111111111111111111111"}},"space":165}}}""");
            var options = new RpcContextOptions
            {
                Commitment = Commitment.Finalized,
                MinContextSlot = 42
            };

            // Act
            var result = await client.GetParsedAccountInfoWithOptionsAsync(PublicKey.Parse(Address), options);

            // Assert
            result.Should().NotBeNull();
            result.Owner.Should().Be(PublicKey.Parse(Program));
            result.Program.Should().Be("spl-token");
            result.Parsed.Should().NotBeNull();
            result.Parsed!.Info.GetProperty("mint").GetString().Should().Be(Address);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"jsonParsed","commitment":"finalized","minContextSlot":42}]}""");
        }

        [Test]
        public async Task NullOptions_IsRejectedBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make("null");

            // Act
            Func<Task> act = async () => await client.GetParsedAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address), null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("options");
            handler.CapturedRequestBody.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByDelegateWithContextAsync
    {
        [Test]
        public async Task MintFilterAndDefaults_ParseContextAndUsePinnedWireShape()
        {
            // Arrange
            var (client, handler) = Make(
                $$"""{"context":{"slot":53},"value":[{{ProgramAccountJson}}]}""");

            // Act
            var result = await client.GetTokenAccountsByDelegateWithContextAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByMint(PublicKey.Parse(Address)));

            // Assert
            result.Context.Slot.Should().Be(53);
            result.Value.Should().ContainSingle();
            result.Value![0].Account.Lamports.Should().Be(2039280);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByDelegate","params":["11111111111111111111111111111111",{"mint":"11111111111111111111111111111111"},{"encoding":"base64"}]}""");
        }

        [Test]
        public async Task NullFilter_IsRejectedBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make("null");

            // Act
            Func<Task> act = async () => await client.GetTokenAccountsByDelegateWithContextAsync(
                PublicKey.Parse(Address), null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("filter");
            handler.CapturedRequestBody.Should().BeNull();
        }
    }
}
