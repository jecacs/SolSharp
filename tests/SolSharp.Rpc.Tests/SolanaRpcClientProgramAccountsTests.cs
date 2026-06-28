using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientProgramAccountsTests
{
    private const string ProgramId = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";
    private const string AccountAddress = "11111111111111111111111111111111";

    // "AQID" is base64 for the bytes [1, 2, 3].
    private const string EntryJson =
        """{"pubkey":"11111111111111111111111111111111","account":{"data":["AQID","base64"],"executable":false,"lamports":42,"owner":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA","rentEpoch":0,"space":3}}""";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    [TestFixture]
    public sealed class GetProgramAccountsAsync
    {
        [Test]
        public async Task ParsesEntriesAndRequestsBase64()
        {
            // Arrange
            var (client, handler) = Make($$"""{"jsonrpc":"2.0","result":[{{EntryJson}}],"id":1}""");

            // Act
            var accounts = await client.GetProgramAccountsAsync(PublicKey.Parse(ProgramId));

            // Assert
            byte[] expectedData = [1, 2, 3];
            accounts.Should().ContainSingle();
            accounts[0].PublicKey.Should().Be(PublicKey.Parse(AccountAddress));
            accounts[0].Account.Lamports.Should().Be(42);
            accounts[0].Account.Data.Should().Equal(expectedData);

            handler.CapturedRequestBody.Should().Contain("\"getProgramAccounts\"");
            handler.CapturedRequestBody.Should().Contain(ProgramId);
            handler.CapturedRequestBody.Should().Contain("\"base64\"");
            handler.CapturedRequestBody.Should().NotContain("filters");
        }

        [Test]
        public async Task SendsMemcmpAndDataSizeFilters()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":[],"id":1}""");
            var options = new GetProgramAccountsOptions
            {
                Filters = [AccountFilter.MemoryCompare(0, "3Mc6vR"), AccountFilter.DataSize(165)]
            };

            // Act
            var accounts = await client.GetProgramAccountsAsync(PublicKey.Parse(ProgramId), options);

            // Assert
            accounts.Should().BeEmpty();
            handler.CapturedRequestBody.Should().Contain("\"filters\"");
            handler.CapturedRequestBody.Should().Contain("\"memcmp\"");
            handler.CapturedRequestBody.Should().Contain("\"offset\":0");
            handler.CapturedRequestBody.Should().Contain("\"bytes\":\"3Mc6vR\"");
            handler.CapturedRequestBody.Should().Contain("\"dataSize\":165");
        }

        [Test]
        public async Task SendsDataSlice()
        {
            // Arrange
            var (client, handler) = Make("""{"jsonrpc":"2.0","result":[],"id":1}""");
            var options = new GetProgramAccountsOptions { DataSlice = new DataSlice(8, 32) };

            // Act
            await client.GetProgramAccountsAsync(PublicKey.Parse(ProgramId), options);

            // Assert
            handler.CapturedRequestBody.Should().Contain("\"dataSlice\":{\"offset\":8,\"length\":32}");
        }
    }
}
