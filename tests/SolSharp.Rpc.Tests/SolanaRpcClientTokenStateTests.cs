using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;

namespace SolSharp.Rpc.Tests;

public static class SolanaRpcClientTokenStateTests
{
    // Reference bytes built with spl.token._layouts and verified against solders.token.state:
    // mint_authority [1]*32, supply 1_000_000, decimals 6, initialized, no freeze authority.
    private const string MintBase64 =
        "AQAAAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBQEIPAAAAAAAGAQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==";

    // mint [2]*32, owner [3]*32, amount 5_000_000, delegate [4]*32, Initialized, is_native 2_039_280,
    // delegated_amount 1_000, no close authority.
    private const string TokenAccountBase64 =
        "AgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDA0BLTAAAAAAAAQAAAAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEAQEAAADwHR8AAAAAAOgDAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return (new SolanaRpcClient(http), handler);
    }

    private static string AccountEnvelope(string dataBase64) =>
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["__DATA__","base64"],"executable":false,"lamports":1,"owner":"11111111111111111111111111111111","rentEpoch":0,"space":0}},"id":1}"""
            .Replace("__DATA__", dataBase64);

    [TestFixture]
    public sealed class MintDecode
    {
        [Test]
        public void DecodesMint_MatchingSolders()
        {
            var mint = Mint.Decode(Convert.FromBase64String(MintBase64));

            mint.Should().NotBeNull();
            mint!.MintAuthority.Should().Be(Pk(1));
            mint.Supply.Should().Be(1_000_000ul);
            mint.Decimals.Should().Be(6);
            mint.IsInitialized.Should().BeTrue();
            mint.FreezeAuthority.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class TokenAccountDecode
    {
        [Test]
        public void DecodesTokenAccount_MatchingSolders()
        {
            var account = TokenAccount.Decode(Convert.FromBase64String(TokenAccountBase64));

            account.Should().NotBeNull();
            account!.Mint.Should().Be(Pk(2));
            account.Owner.Should().Be(Pk(3));
            account.Amount.Should().Be(5_000_000ul);
            account.Delegate.Should().Be(Pk(4));
            account.State.Should().Be(TokenAccountState.Initialized);
            account.IsNative.Should().Be(2_039_280ul);
            account.IsNativeAccount.Should().BeTrue();
            account.DelegatedAmount.Should().Be(1_000ul);
            account.CloseAuthority.Should().BeNull();
            account.IsFrozen.Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class GetMintAsync
    {
        [Test]
        public async Task FetchesAndDecodes()
        {
            var (client, handler) = Make(AccountEnvelope(MintBase64));

            var mint = await client.GetMintAsync(Pk(1));

            mint.Should().NotBeNull();
            mint!.Decimals.Should().Be(6);
            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountAsync
    {
        [Test]
        public async Task FetchesAndDecodes()
        {
            var (client, _) = Make(AccountEnvelope(TokenAccountBase64));

            var account = await client.GetTokenAccountAsync(Pk(2));

            account.Should().NotBeNull();
            account!.Amount.Should().Be(5_000_000ul);
        }
    }
}
