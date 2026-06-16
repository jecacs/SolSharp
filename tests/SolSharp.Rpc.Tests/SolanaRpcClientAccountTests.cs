using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientAccountTests
{
    private const string OwnerBase58 = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    // "AQID" is base64 for the bytes [1, 2, 3]; rentEpoch is u64 max, as the node reports for rent-exempt accounts.
    private const string AccountValueJson =
        """{"data":["AQID","base64"],"executable":false,"lamports":2039280,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":18446744073709551615,"space":3}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static string ContextEnvelope(string valueJson) =>
        $$"""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{{valueJson}}},"id":1}""";

    [TestFixture]
    public sealed class GetAccountInfoAsync
    {
        [Test]
        public async Task ParsesTheAccountAndDecodesBase64Data()
        {
            var (client, handler) = Make(ContextEnvelope(AccountValueJson));

            var info = await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            byte[] expectedData = [1, 2, 3];
            info.Should().NotBeNull();
            info!.Lamports.Should().Be(2039280);
            info.Owner.Should().Be(PublicKey.Parse(OwnerBase58));
            info.Executable.Should().BeFalse();
            info.RentEpoch.Should().Be(ulong.MaxValue);
            info.Data.Should().Equal(expectedData);

            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
            handler.CapturedRequestBody.Should().Contain(OwnerBase58);
        }

        [Test]
        public async Task ReturnsNullWhenTheAccountDoesNotExist()
        {
            var (client, _) = Make(ContextEnvelope("null"));

            var info = await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            info.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsAsync
    {
        [Test]
        public async Task ParsesEachAccountAndPreservesNullSlots()
        {
            var (client, handler) = Make(ContextEnvelope($"[{AccountValueJson},null]"));
            PublicKey[] accounts = [PublicKey.Parse(OwnerBase58), PublicKey.Parse("11111111111111111111111111111111")];

            var infos = await client.GetMultipleAccountsAsync(accounts);

            infos.Should().HaveCount(2);
            infos[0].Should().NotBeNull();
            infos[0]!.Lamports.Should().Be(2039280);
            infos[1].Should().BeNull();

            handler.CapturedRequestBody.Should().Contain("\"getMultipleAccounts\"");
            handler.CapturedRequestBody.Should().Contain(OwnerBase58);
        }

        [Test]
        public async Task ThrowsWhenAccountsIsNull()
        {
            var (client, _) = Make(ContextEnvelope("[]"));

            Func<Task> act = () => client.GetMultipleAccountsAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
