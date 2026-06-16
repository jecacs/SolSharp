using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientTokenAccountsTests
{
    private const string Owner = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string Mint = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

    private const string EntryJson =
        """{"pubkey":"11111111111111111111111111111111","account":{"data":["AQID","base64"],"executable":false,"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":165}}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static string ContextArray(string valueJson) =>
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":__VALUE__},"id":1}"""
            .Replace("__VALUE__", valueJson);

    [TestFixture]
    public sealed class GetTokenAccountsByOwnerAsync
    {
        [Test]
        public async Task ParsesAccountsAndFiltersByMint()
        {
            var (client, handler) = Make(ContextArray($"[{EntryJson}]"));

            var accounts = await client.GetTokenAccountsByOwnerAsync(PublicKey.Parse(Owner), PublicKey.Parse(Mint));

            accounts.Should().ContainSingle();
            accounts[0].Account.Lamports.Should().Be(2039280);
            handler.CapturedRequestBody.Should().Contain("\"getTokenAccountsByOwner\"");
            handler.CapturedRequestBody.Should().Contain(Owner);
            handler.CapturedRequestBody.Should().Contain(Mint);
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetRecentPrioritizationFeesAsync
    {
        [Test]
        public async Task ParsesFees()
        {
            var (client, handler) = Make(
                """{"jsonrpc":"2.0","result":[{"slot":100,"prioritizationFee":5000},{"slot":101,"prioritizationFee":0}],"id":1}""");

            var fees = await client.GetRecentPrioritizationFeesAsync();

            fees.Should().HaveCount(2);
            fees[0].Slot.Should().Be(100);
            fees[0].Fee.Should().Be(5000);
            fees[1].Fee.Should().Be(0);
            handler.CapturedRequestBody.Should().Contain("\"getRecentPrioritizationFees\"");
        }
    }
}
