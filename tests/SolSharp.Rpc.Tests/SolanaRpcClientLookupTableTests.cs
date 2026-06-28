using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientLookupTableTests
{
    private const string TableAddress = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    // Authoritative ALT account data (verified against solders AddressLookupTable.deserialize): an active
    // table, last_extended_slot 123, authority [9]*32, addresses [2]*32 and [3]*32.
    private const string TableDataBase64 =
        "AQAAAP//////////ewAAAAAAAAAAAQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJCQkJAAACAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMD";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static string AccountEnvelope(string dataBase64) =>
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["__DATA__","base64"],"executable":false,"lamports":1,"owner":"11111111111111111111111111111111","rentEpoch":0,"space":120}},"id":1}"""
            .Replace("__DATA__", dataBase64);

    [TestFixture]
    public sealed class GetAddressLookupTableAsync
    {
        [Test]
        public async Task DecodesActiveTable()
        {
            // Arrange
            var (client, handler) = Make(AccountEnvelope(TableDataBase64));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().NotBeNull();
            table!.IsActive.Should().BeTrue();
            table.DeactivationSlot.Should().Be(ulong.MaxValue);
            table.LastExtendedSlot.Should().Be(123);
            table.Authority.Should().Be(PublicKey.Parse("cGfHiC6Kgg3FpFZvgwGcswsCRtp4aBP2fzuXRQPizuN"));
            table.Addresses.Should().HaveCount(2);
            table.Addresses[0].Should().Be(PublicKey.Parse("8qbHbw2BbbTHBW1sbeqakYXVKRQM8Ne7pLK7m6CVfeR"));
            table.Addresses[1].Should().Be(PublicKey.Parse("CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8"));

            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
            handler.CapturedRequestBody.Should().Contain(TableAddress);
        }

        [Test]
        public async Task ReturnsNullWhenAccountMissing()
        {
            // Arrange
            var (client, _) = Make("""{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":null},"id":1}""");

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().BeNull();
        }

        [Test]
        public async Task ReturnsNullWhenDataIsNotALookupTable()
        {
            // Four zero bytes: shorter than the metadata and discriminant 0, so not an initialized table.
            // Arrange
            var (client, _) = Make(AccountEnvelope("AAAAAA=="));

            // Act
            var table = await client.GetAddressLookupTableAsync(PublicKey.Parse(TableAddress));

            // Assert
            table.Should().BeNull();
        }
    }
}
