using System.Text.Json;
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
            // Arrange
            var (client, handler) = Make(ContextEnvelope(AccountValueJson));

            // Act
            var info = await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            // Assert
            byte[] expectedData = [1, 2, 3];
            info.Should().NotBeNull();
            info!.Lamports.Should().Be(2039280);
            info.Owner.Should().Be(PublicKey.Parse(OwnerBase58));
            info.Executable.Should().BeFalse();
            info.RentEpoch.Should().Be(ulong.MaxValue);
            info.Space.Should().Be(3);
            info.Data.Should().Equal(expectedData);

            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
            handler.CapturedRequestBody.Should().Contain(OwnerBase58);
        }

        [Test]
        public async Task ReturnsNullWhenTheAccountDoesNotExist()
        {
            // Arrange
            var (client, _) = Make(ContextEnvelope("null"));

            // Act
            var info = await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            // Assert
            info.Should().BeNull();
        }

        [Test]
        public async Task SendsDataSlice()
        {
            // Arrange
            var (client, handler) = Make(ContextEnvelope(AccountValueJson));

            // Act
            await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58), dataSlice: new DataSlice(8, 32));

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"dataSlice\":{\"offset\":8,\"length\":32}");
        }

        [TestCase("[\"AQID\"]")]
        [TestCase("[\"AQID\",\"base58\"]")]
        [TestCase("[\"AQID\",\"base64\",\"extra\"]")]
        [TestCase("{\"bytes\":\"AQID\"}")]
        public async Task MalformedOrUnsupportedDataTuple_ThrowsJsonException(string data)
        {
            var value =
                """{"data":__DATA__,"executable":false,"lamports":1,"owner":"11111111111111111111111111111111","rentEpoch":0}"""
                    .Replace("__DATA__", data);
            var (client, _) = Make(ContextEnvelope(value));

            Func<Task> act = async () => await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            await act.Should().ThrowAsync<JsonException>();
        }

        [TestCase("\"oops\"")]
        [TestCase("true")]
        [TestCase("{}")]
        [TestCase("-1")]
        [TestCase("18446744073709551616")]
        public async Task PresentSpaceOutsideOptionalU64_ThrowsJsonException(string space)
        {
            // Arrange
            var value =
                """{"data":["AQID","base64"],"executable":false,"lamports":1,"owner":"11111111111111111111111111111111","rentEpoch":0,"space":__SPACE__}"""
                    .Replace("__SPACE__", space, StringComparison.Ordinal);
            var (client, _) = Make(ContextEnvelope(value));

            // Act
            var act = async () => await client.GetAccountInfoAsync(PublicKey.Parse(OwnerBase58));

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsAsync
    {
        [Test]
        public async Task ParsesEachAccountAndPreservesNullSlots()
        {
            // Arrange
            var (client, handler) = Make(ContextEnvelope($"[{AccountValueJson},null]"));
            PublicKey[] accounts = [PublicKey.Parse(OwnerBase58), PublicKey.Parse("11111111111111111111111111111111")];

            // Act
            var infos = await client.GetMultipleAccountsAsync(accounts);

            // Assert
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
            // Arrange
            var (client, _) = Make(ContextEnvelope("[]"));

            // Act
            Func<Task> act = () => client.GetMultipleAccountsAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }
    }
}
