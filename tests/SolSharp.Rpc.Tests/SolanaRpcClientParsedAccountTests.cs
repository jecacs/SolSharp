using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientParsedAccountTests
{
    private const string Token = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Usdc = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";
    private const string Owner = "67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetParsedAccountInfoAsync
    {
        [Test]
        public async Task ParsesRecognizedTokenAccount()
        {
            // Arrange
            var (client, handler) = Make(TokenAccountJson);

            // Act
            var account = await client.GetParsedAccountInfoAsync(PublicKey.Parse(Owner));

            // Assert
            account.Should().NotBeNull();
            account!.Owner.Should().Be(PublicKey.Parse(Token));
            account.Lamports.Should().Be(2039280ul);
            account.Space.Should().Be(165ul);
            account.Program.Should().Be("spl-token");
            account.Parsed.Should().NotBeNull();
            account.Parsed!.Type.Should().Be("account");
            account.Parsed.Info.GetProperty("mint").GetString().Should().Be(Usdc);
            account.RawData.Should().BeNull();

            handler.CapturedRequestBody.Should().Contain("getAccountInfo");
            handler.CapturedRequestBody.Should().Contain("jsonParsed");
        }

        [Test]
        public async Task FallsBackToRawBytesForUnrecognizedProgram()
        {
            // Arrange
            var (client, _) = Make(RawAccountJson);

            // Act
            var account = await client.GetParsedAccountInfoAsync(PublicKey.Parse(Owner));

            // Assert
            account.Should().NotBeNull();
            account!.Program.Should().BeNull();
            account.Parsed.Should().BeNull();
            account.RawData.Should().Equal((byte)1, 2, 3, 4);
        }

        [Test]
        public async Task ReturnsNullWhenNotFound()
        {
            // Arrange
            var (client, _) = Make(NullAccountJson);

            // Act & Assert
            (await client.GetParsedAccountInfoAsync(PublicKey.Parse(Owner))).Should().BeNull();
        }
    }

    private const string TokenAccountJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":250},"value":{"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","executable":false,"rentEpoch":18446744073709551615,"space":165,"data":{"program":"spl-token","parsed":{"type":"account","info":{"mint":"EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v","owner":"67vHA8qZGCJKw1UNGUJZME4MwEWDRGWzp7MGvsut43A8","tokenAmount":{"amount":"1000000","decimals":6,"uiAmount":1.0,"uiAmountString":"1"},"state":"initialized"}},"space":165}}},"id":1}""";

    private const string RawAccountJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":250},"value":{"lamports":5000,"owner":"3x9az88Dkbxa6tkKByxqEn7jBTJCJCD4dVvou49L24ET","executable":false,"rentEpoch":18446744073709551615,"space":4,"data":["AQIDBA==","base64"]}},"id":1}""";

    private const string NullAccountJson =
        """{"jsonrpc":"2.0","result":{"context":{"slot":250},"value":null},"id":1}""";
}
