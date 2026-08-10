using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Protocol;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientAccountEncodingTests
{
    private const string Address = "11111111111111111111111111111111";
    private const string TokenProgram = "TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA";

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string resultJson)
    {
        var handler = new FakeHttpMessageHandler(
            $$"""{"jsonrpc":"2.0","result":{{resultJson}},"id":1}""");
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    private static string Account(string data) =>
        """{"lamports":42,"owner":"11111111111111111111111111111111","executable":false,"rentEpoch":9,"space":3,"data":__DATA__}"""
            .Replace("__DATA__", data);

    private static string Contextual(string account) =>
        """{"context":{"slot":7},"value":__ACCOUNT__}"""
            .Replace("__ACCOUNT__", account);

    private static string ContextualAccount(string data) => Contextual(Account(data));

    private static string KeyedAccount(string data) =>
        """{"pubkey":"11111111111111111111111111111111","account":__ACCOUNT__}"""
            .Replace("__ACCOUNT__", Account(data));

    [TestFixture]
    public sealed class GetAccountInfoWithOptionsAndContextAsync
    {
        [Test]
        public async Task ContextMethod_PreservesSlotAndExactDataBranch()
        {
            // Arrange
            var (client, handler) = Make(ContextualAccount("[\"AQID\",\"base64\"]"));

            // Act
            var result = await client.GetAccountInfoWithOptionsAndContextAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.Base64, MinContextSlot = 6 });

            // Assert
            result.Context.Slot.Should().Be(7);
            result.Value!.Data.Should().BeOfType<RpcAccountData.Encoded>();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"base64","minContextSlot":6}]}""");
        }
    }

    [TestFixture]
    public sealed class GetAccountInfoWithOptionsAsync
    {
        [Test]
        public async Task Binary_ParsesLegacyBareStringAndSendsExactWireName()
        {
            // Arrange
            var (client, handler) = Make(ContextualAccount("\"3Mc6vR\""));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.Binary });

            // Assert
            account!.Data.Should().BeOfType<RpcAccountData.LegacyBinary>()
                .Which.EncodedData.Should().Be("3Mc6vR");
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"binary"}]}""");
        }

        [TestCase(RpcAccountEncoding.Base58, "base58", "3Mc6vR")]
        [TestCase(RpcAccountEncoding.Base64, "base64", "AQID")]
        [TestCase(RpcAccountEncoding.Base64Zstd, "base64+zstd", "KLUv_Q")]
        public async Task ExplicitEncoding_ParsesTaggedTuple(
            RpcAccountEncoding requestedEncoding,
            string wireEncoding,
            string encodedData)
        {
            // Arrange
            var (client, handler) = Make(ContextualAccount($$"""["{{encodedData}}","{{wireEncoding}}"]"""));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = requestedEncoding });

            // Assert
            var data = account!.Data.Should().BeOfType<RpcAccountData.Encoded>().Which;
            data.Encoding.Should().Be(requestedEncoding);
            data.EncodedData.Should().Be(encodedData);
            var serializedWireEncoding = wireEncoding.Replace("+", "\\u002B");
            handler.CapturedRequestBody.Should().Contain($"\"encoding\":\"{serializedWireEncoding}\"");
        }

        [Test]
        public async Task JsonParsed_ParsesProgramSpecificPayloadWithoutProjection()
        {
            // Arrange
            const string parsedData =
                """{"program":"spl-token","parsed":{"type":"mint","info":{"decimals":6}},"space":82}""";
            var (client, handler) = Make(ContextualAccount(parsedData));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new()
                {
                    Encoding = RpcAccountEncoding.JsonParsed,
                    Commitment = Commitment.Finalized,
                    DataSlice = new(0, 8),
                    MinContextSlot = 6
                });

            // Assert
            var data = account!.Data.Should().BeOfType<RpcAccountData.Parsed>().Which;
            data.Program.Should().Be("spl-token");
            data.Space.Should().Be(82);
            data.Value.GetProperty("info").GetProperty("decimals").GetInt32().Should().Be(6);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getAccountInfo","params":["11111111111111111111111111111111",{"encoding":"jsonParsed","commitment":"finalized","dataSlice":{"offset":0,"length":8},"minContextSlot":6}]}""");
        }

        [Test]
        public async Task JsonParsedUnknownProgram_PreservesUpstreamBase64Fallback()
        {
            // Arrange
            var (client, _) = Make(ContextualAccount("[\"AQID\",\"base64\"]"));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.JsonParsed });

            // Assert
            var data = account!.Data.Should().BeOfType<RpcAccountData.Encoded>().Which;
            data.Encoding.Should().Be(RpcAccountEncoding.Base64);
            data.EncodedData.Should().Be("AQID");
        }

        [Test]
        public async Task UnsetEncoding_OmitsFieldAndAcceptsMethodDefaultBranch()
        {
            // Arrange
            var (client, handler) = Make(ContextualAccount("\"3Mc6vR\""));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { MinContextSlot = 6 });

            // Assert
            account!.Data.Should().BeOfType<RpcAccountData.LegacyBinary>();
            handler.CapturedRequestBody.Should().NotContain("encoding");
            handler.CapturedRequestBody.Should().Contain("\"minContextSlot\":6");
        }

        [Test]
        public async Task UnknownRequestedEncoding_ThrowsBeforeTransport()
        {
            // Arrange
            var (client, handler) = Make("null");
            var options = new RpcAccountInfoOptions { Encoding = (RpcAccountEncoding)int.MaxValue };

            // Act
            var act = async () => await client.GetAccountInfoWithOptionsAsync(PublicKey.Parse(Address), options);

            // Assert
            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
            handler.CapturedRequestBody.Should().BeNull();
        }

        [TestCase("null")]
        [TestCase("[]")]
        [TestCase("[\"AQID\"]")]
        [TestCase("[\"AQID\",\"unknown\"]")]
        [TestCase("[\"AQID\",1]")]
        [TestCase("{}")]
        [TestCase("{\"program\":\"spl-token\",\"parsed\":{},\"space\":-1}")]
        [TestCase("{\"program\":1,\"parsed\":{},\"space\":82}")]
        public async Task MalformedAccountData_ThrowsJsonException(string data)
        {
            // Arrange
            var (client, _) = Make(ContextualAccount(data));

            // Act
            var act = async () => await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address), new());

            // Assert
            await act.Should().ThrowAsync<JsonException>();
        }

        [TestCase("lamports", "\"lamports\":42,")]
        [TestCase("data", ",\"data\":[\"AQID\",\"base64\"]")]
        [TestCase("owner", "\"owner\":\"11111111111111111111111111111111\",")]
        [TestCase("executable", "\"executable\":false,")]
        [TestCase("rentEpoch", "\"rentEpoch\":9,")]
        public async Task OmittedMandatoryAccountField_ThrowsJsonException(
            string omittedField,
            string propertyFragment)
        {
            // Arrange
            var malformed = Account("[\"AQID\",\"base64\"]")
                .Replace(propertyFragment, string.Empty, StringComparison.Ordinal);
            var (client, _) = Make(Contextual(malformed));

            // Act
            var act = async () => await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            await act.Should().ThrowAsync<JsonException>().WithMessage($"*{omittedField}*");
        }

        [Test]
        public async Task OmittedOptionalSpace_ParsesWithNullSpace()
        {
            // Arrange
            var withoutSpace = Account("[\"AQID\",\"base64\"]")
                .Replace("\"space\":3,", string.Empty, StringComparison.Ordinal);
            var (client, _) = Make(Contextual(withoutSpace));

            // Act
            var account = await client.GetAccountInfoWithOptionsAsync(
                PublicKey.Parse(Address),
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            account.Should().NotBeNull();
            account.Lamports.Should().Be(42);
            account.Owner.Should().Be(PublicKey.Parse(Address));
            account.Executable.Should().BeFalse();
            account.RentEpoch.Should().Be(9);
            account.Data.Should().BeOfType<RpcAccountData.Encoded>();
            account.Space.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsWithOptionsAndContextAsync
    {
        [Test]
        public async Task ContextMethod_PreservesSlotAndMissingEntries()
        {
            // Arrange
            var resultJson = $$"""{"context":{"slot":7},"value":[null,{{Account("[\"AQID\",\"base64\"]")}}]}""";
            var (client, handler) = Make(resultJson);

            // Act
            var result = await client.GetMultipleAccountsWithOptionsAndContextAsync(
                [PublicKey.Parse(Address), PublicKey.Parse(Address)],
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            result.Context.Slot.Should().Be(7);
            result.Value.Should().HaveCount(2);
            result.Value![0].Should().BeNull();
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetMultipleAccountsWithOptionsAsync
    {
        [Test]
        public async Task PreservesNullEntriesAndExactTupleBranches()
        {
            // Arrange
            var result = $$"""{"context":{"slot":7},"value":[null,{{Account("[\"AQID\",\"base64\"]")}}]}""";
            var (client, handler) = Make(result);

            // Act
            var accounts = await client.GetMultipleAccountsWithOptionsAsync(
                [PublicKey.Parse(Address), PublicKey.Parse(Address)],
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            accounts.Should().HaveCount(2);
            accounts[0].Should().BeNull();
            accounts[1]!.Data.Should().BeOfType<RpcAccountData.Encoded>()
                .Which.Encoding.Should().Be(RpcAccountEncoding.Base64);
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetProgramAccountsWithOptionsAndContextAsync
    {
        [Test]
        public async Task ContextMethod_ForcesUpstreamContextShape()
        {
            // Arrange
            var resultJson = $$"""{"context":{"slot":9},"value":[{{KeyedAccount("[\"AQID\",\"base64\"]")}}]}""";
            var (client, handler) = Make(resultJson);

            // Act
            var result = await client.GetProgramAccountsWithOptionsAndContextAsync(
                PublicKey.Parse(TokenProgram),
                new() { Encoding = RpcAccountEncoding.Base64, WithContext = false });

            // Assert
            result.Context.Slot.Should().Be(9);
            result.Value.Should().ContainSingle();
            handler.CapturedRequestBody.Should().Contain("\"withContext\":true");
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetProgramAccountsWithOptionsAsync
    {
        [Test]
        public async Task ContextShape_ParsesKeyedParsedAccountAndSendsAllOptions()
        {
            // Arrange
            const string parsedData = """{"program":"spl-token","parsed":{"type":"mint"},"space":82}""";
            var result = $$"""{"context":{"slot":9},"value":[{{KeyedAccount(parsedData)}}]}""";
            var (client, handler) = Make(result);

            // Act
            var accounts = await client.GetProgramAccountsWithOptionsAsync(
                PublicKey.Parse(TokenProgram),
                new()
                {
                    Encoding = RpcAccountEncoding.JsonParsed,
                    Commitment = Commitment.Finalized,
                    Filters = [AccountFilter.DataSize(82)],
                    DataSlice = new(0, 8),
                    MinContextSlot = 8,
                    WithContext = true,
                    SortResults = false
                });

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Account.Data.Should().BeOfType<RpcAccountData.Parsed>();
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getProgramAccounts","params":["TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA",{"encoding":"jsonParsed","commitment":"finalized","minContextSlot":8,"dataSlice":{"offset":0,"length":8},"filters":[{"dataSize":82}],"withContext":true,"sortResults":false}]}""");
        }

        [Test]
        public async Task BareShape_ParsesWithoutContext()
        {
            // Arrange
            var result = $$"""[{{KeyedAccount("[\"3Mc6vR\",\"base58\"]")}}]""";
            var (client, _) = Make(result);

            // Act
            var accounts = await client.GetProgramAccountsWithOptionsAsync(
                PublicKey.Parse(TokenProgram),
                new() { Encoding = RpcAccountEncoding.Base58 });

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Account.Data.Should().BeOfType<RpcAccountData.Encoded>()
                .Which.Encoding.Should().Be(RpcAccountEncoding.Base58);
        }

        [Test]
        public async Task ExplicitNullAccount_ThrowsJsonException()
        {
            // Arrange
            var result = $$"""[{"pubkey":"{{Address}}","account":null}]""";
            var (client, _) = Make(result);

            // Act
            var act = async () => await client.GetProgramAccountsWithOptionsAsync(
                PublicKey.Parse(TokenProgram),
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            await act.Should().ThrowAsync<JsonException>().WithMessage("*non-null account*");
        }

        [Test]
        public async Task NullKeyedAccountEntry_ThrowsJsonException()
        {
            // Arrange
            var (client, _) = Make("[null]");

            // Act
            var act = async () => await client.GetProgramAccountsWithOptionsAsync(
                PublicKey.Parse(TokenProgram),
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            await act.Should().ThrowAsync<JsonException>().WithMessage("*cannot contain null entries*");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByOwnerWithOptionsAndContextAsync
    {
        [Test]
        public async Task OwnerContextMethod_PreservesMandatoryContext()
        {
            // Arrange
            var resultJson = $$"""{"context":{"slot":9},"value":[{{KeyedAccount("[\"AQID\",\"base64\"]")}}]}""";
            var (client, handler) = Make(resultJson);

            // Act
            var result = await client.GetTokenAccountsByOwnerWithOptionsAndContextAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByProgramId(PublicKey.Parse(TokenProgram)),
                new() { Encoding = RpcAccountEncoding.Base64 });

            // Assert
            result.Context.Slot.Should().Be(9);
            result.Value.Should().ContainSingle();
            handler.CapturedRequestBody.Should().Contain("\"programId\":\"Tokenkeg");
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base64\"");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByDelegateWithOptionsAndContextAsync
    {
        [Test]
        public async Task DelegateContextMethod_PreservesMandatoryContext()
        {
            // Arrange
            var resultJson = $$"""{"context":{"slot":10},"value":[{{KeyedAccount("[\"3Mc6vR\",\"base58\"]")}}]}""";
            var (client, handler) = Make(resultJson);

            // Act
            var result = await client.GetTokenAccountsByDelegateWithOptionsAndContextAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByMint(PublicKey.Parse(Address)),
                new() { Encoding = RpcAccountEncoding.Base58 });

            // Assert
            result.Context.Slot.Should().Be(10);
            result.Value.Should().ContainSingle();
            handler.CapturedRequestBody.Should().Contain("\"mint\":\"11111111111111111111111111111111\"");
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"base58\"");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByOwnerWithOptionsAsync
    {
        [Test]
        public async Task OwnerPath_SendsProgramFilterAndParsesBase64Zstd()
        {
            // Arrange
            var result = $$"""{"context":{"slot":9},"value":[{{KeyedAccount("[\"KLUv_Q\",\"base64+zstd\"]")}}]}""";
            var (client, handler) = Make(result);

            // Act
            var accounts = await client.GetTokenAccountsByOwnerWithOptionsAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByProgramId(PublicKey.Parse(TokenProgram)),
                new() { Encoding = RpcAccountEncoding.Base64Zstd });

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Account.Data.Should().BeOfType<RpcAccountData.Encoded>()
                .Which.Encoding.Should().Be(RpcAccountEncoding.Base64Zstd);
            handler.CapturedRequestBody.Should().Be(
                """{"jsonrpc":"2.0","id":1,"method":"getTokenAccountsByOwner","params":["11111111111111111111111111111111",{"programId":"TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA"},{"encoding":"base64\u002Bzstd"}]}""");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountsByDelegateWithOptionsAsync
    {
        [Test]
        public async Task DelegatePath_SendsMintFilterAndParsesLegacyBinary()
        {
            // Arrange
            var result = $$"""{"context":{"slot":9},"value":[{{KeyedAccount("\"3Mc6vR\"")}}]}""";
            var (client, handler) = Make(result);

            // Act
            var accounts = await client.GetTokenAccountsByDelegateWithOptionsAsync(
                PublicKey.Parse(Address),
                TokenAccountsFilter.ByMint(PublicKey.Parse(Address)),
                new() { Encoding = RpcAccountEncoding.Binary });

            // Assert
            accounts.Should().ContainSingle();
            accounts[0].Account.Data.Should().BeOfType<RpcAccountData.LegacyBinary>();
            handler.CapturedRequestBody.Should().Contain("\"mint\":\"11111111111111111111111111111111\"");
            handler.CapturedRequestBody.Should().Contain("\"encoding\":\"binary\"");
        }
    }
}

public static class RpcAccountDataJsonConverterTests
{
    private static string Account(string data) =>
        """{"lamports":42,"owner":"11111111111111111111111111111111","executable":false,"rentEpoch":9,"space":3,"data":__DATA__}"""
            .Replace("__DATA__", data);

    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void NullBranch_ThrowsJsonExceptionWhenWritten()
        {
            // Act
            var act = static () => JsonSerializer.Serialize<RpcAccountData>(null!, RpcJson.Options);

            // Assert
            act.Should().Throw<JsonException>().WithMessage("Account data cannot be null.");
        }

        [Test]
        public void SourceGeneratedRoundTrip_PreservesParsedPayload()
        {
            // Arrange
            var json = Account("{\"program\":\"spl-token\",\"parsed\":null,\"space\":82}");
            var account = JsonSerializer.Deserialize<RpcAccountInfo>(json, RpcJson.Options)!;

            // Act
            var roundTrip = JsonSerializer.Serialize(account, RpcJson.Options);

            // Assert
            account.Data.Should().BeOfType<RpcAccountData.Parsed>().Which.Value.ValueKind
                .Should().Be(JsonValueKind.Null);
            roundTrip.Should().Contain("\"data\":{\"program\":\"spl-token\",\"parsed\":null,\"space\":82}");
        }
    }

    [TestFixture]
    public sealed class Read
    {
        [Test]
        public void ExternalConsumerContext_ResolvesPublicConverterAndUnion()
        {
            // Arrange
            var json = Account("[\"AQID\",\"base64\"]");

            // Act
            var account = JsonSerializer.Deserialize(json, ConsumerAccountJsonContext.Default.RpcAccountInfo)!;
            var roundTrip = JsonSerializer.Serialize(account, ConsumerAccountJsonContext.Default.RpcAccountInfo);

            // Assert
            account.Data.Should().BeOfType<RpcAccountData.Encoded>().Which.Encoding
                .Should().Be(RpcAccountEncoding.Base64);
            roundTrip.Should().Contain("\"data\":[\"AQID\",\"base64\"]");
        }
    }
}

[JsonSerializable(typeof(RpcAccountInfo))]
internal sealed partial class ConsumerAccountJsonContext : JsonSerializerContext;
