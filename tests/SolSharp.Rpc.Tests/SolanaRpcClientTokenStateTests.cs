using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
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
        return new(bytes);
    }

    private static (SolanaRpcClient Client, FakeHttpMessageHandler Handler) Make(string responseJson)
    {
        var handler = new FakeHttpMessageHandler(responseJson);
        var http = new HttpClient(handler) { BaseAddress = new("http://localhost") };
        return (new(http), handler);
    }

    private static string AccountEnvelope(string dataBase64, string owner = SolanaProgramIds.TokenProgram) =>
        """{"jsonrpc":"2.0","result":{"context":{"slot":1},"value":{"data":["__DATA__","base64"],"executable":false,"lamports":1,"owner":"__OWNER__","rentEpoch":0,"space":0}},"id":1}"""
            .Replace("__DATA__", dataBase64)
            .Replace("__OWNER__", owner);

    private static byte[] Token2022Data(string base64, byte accountType)
    {
        var data = new byte[166];
        Convert.FromBase64String(base64).CopyTo(data, 0);
        data[165] = accountType;
        return data;
    }

    [TestFixture]
    public sealed class MintDecode
    {
        [Test]
        public void DecodesMint_MatchingSolders()
        {
            // Act
            var mint = Mint.Decode(Convert.FromBase64String(MintBase64));

            // Assert
            mint.Should().NotBeNull();
            mint.MintAuthority.Should().Be(Pk(1));
            mint.Supply.Should().Be(1_000_000ul);
            mint.Decimals.Should().Be(6);
            mint.IsInitialized.Should().BeTrue();
            mint.FreezeAuthority.Should().BeNull();
        }

        [Test]
        public void TokenAccountLayout_ReturnsNull()
            => Mint.Decode(Convert.FromBase64String(TokenAccountBase64)).Should().BeNull();

        [Test]
        public void InvalidCOptionOrBoolean_ReturnsNull()
        {
            var invalidOption = Convert.FromBase64String(MintBase64);
            BinaryPrimitives.WriteUInt32LittleEndian(invalidOption, 2);
            var invalidBoolean = Convert.FromBase64String(MintBase64);
            invalidBoolean[45] = 2;

            Mint.Decode(invalidOption).Should().BeNull();
            Mint.Decode(invalidBoolean).Should().BeNull();
        }

        [Test]
        public void Token2022NonZeroMintPaddingOrMultisigLength_ReturnsNull()
        {
            var invalidPadding = Token2022Data(MintBase64, accountType: 1);
            invalidPadding[Mint.Length] = 1;
            var multisigLength = new byte[355];
            Convert.FromBase64String(MintBase64).CopyTo(multisigLength, 0);
            multisigLength[165] = 1;

            Mint.Decode(invalidPadding).Should().BeNull();
            Mint.Decode(multisigLength).Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class TokenAccountDecode
    {
        [Test]
        public void DecodesTokenAccount_MatchingSolders()
        {
            // Act
            var account = TokenAccount.Decode(Convert.FromBase64String(TokenAccountBase64));

            // Assert
            account.Should().NotBeNull();
            account.Mint.Should().Be(Pk(2));
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

        [Test]
        public void InvalidCOptionOrState_ReturnsNull()
        {
            var invalidOption = Convert.FromBase64String(TokenAccountBase64);
            BinaryPrimitives.WriteUInt32LittleEndian(invalidOption.AsSpan(72), 2);
            var invalidState = Convert.FromBase64String(TokenAccountBase64);
            invalidState[108] = 3;

            TokenAccount.Decode(invalidOption).Should().BeNull();
            TokenAccount.Decode(invalidState).Should().BeNull();
        }

        [Test]
        public void Token2022MultisigLength_ReturnsNull()
        {
            var data = new byte[355];
            Convert.FromBase64String(TokenAccountBase64).CopyTo(data, 0);
            data[165] = 2;

            TokenAccount.Decode(data).Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetMintAsync
    {
        [Test]
        public async Task FetchesAndDecodes()
        {
            // Arrange
            var (client, handler) = Make(AccountEnvelope(MintBase64));

            // Act
            var mint = await client.GetMintAsync(Pk(1));

            // Assert
            mint.Should().NotBeNull();
            mint.Decimals.Should().Be(6);
            handler.CapturedRequestBody.Should().Contain("\"getAccountInfo\"");
        }

        [Test]
        public async Task WrongOwnerOrTokenAccountLayout_ReturnsNull()
        {
            var (wrongOwnerClient, _) = Make(AccountEnvelope(MintBase64, SolanaProgramIds.SystemProgram));
            var (tokenAccountClient, _) = Make(AccountEnvelope(TokenAccountBase64));

            (await wrongOwnerClient.GetMintAsync(Pk(1))).Should().BeNull();
            (await tokenAccountClient.GetMintAsync(Pk(1))).Should().BeNull();
        }

        [Test]
        public async Task Token2022ExtendedMint_Decodes()
        {
            var data = Token2022Data(MintBase64, accountType: 1);
            var (client, _) = Make(AccountEnvelope(Convert.ToBase64String(data), SolanaProgramIds.Token2022Program));

            var mint = await client.GetMintAsync(Pk(1));

            mint.Should().NotBeNull();
            mint.Decimals.Should().Be(6);
        }
    }

    [TestFixture]
    public sealed class GetTokenAccountAsync
    {
        [Test]
        public async Task FetchesAndDecodes()
        {
            // Arrange
            var (client, _) = Make(AccountEnvelope(TokenAccountBase64));

            // Act
            var account = await client.GetTokenAccountAsync(Pk(2));

            // Assert
            account.Should().NotBeNull();
            account.Amount.Should().Be(5_000_000ul);
        }

        [Test]
        public async Task WrongOwnerOrMintLayout_ReturnsNull()
        {
            var (wrongOwnerClient, _) = Make(AccountEnvelope(TokenAccountBase64, SolanaProgramIds.SystemProgram));
            var extendedMint = Token2022Data(MintBase64, accountType: 1);
            var (mintClient, _) = Make(AccountEnvelope(Convert.ToBase64String(extendedMint), SolanaProgramIds.Token2022Program));

            (await wrongOwnerClient.GetTokenAccountAsync(Pk(2))).Should().BeNull();
            (await mintClient.GetTokenAccountAsync(Pk(2))).Should().BeNull();
        }

        [Test]
        public async Task Token2022ExtendedAccount_Decodes()
        {
            var data = Token2022Data(TokenAccountBase64, accountType: 2);
            var (client, _) = Make(AccountEnvelope(Convert.ToBase64String(data), SolanaProgramIds.Token2022Program));

            var account = await client.GetTokenAccountAsync(Pk(2));

            account.Should().NotBeNull();
            account.Amount.Should().Be(5_000_000ul);
        }
    }
}
