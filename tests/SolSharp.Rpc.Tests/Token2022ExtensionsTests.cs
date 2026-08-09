using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models;
using SolSharp.Rpc.Models.Token2022;

namespace SolSharp.Rpc.Tests;

public static class Token2022ExtensionsTests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    /// <summary>Builds extended account data: 165 padded base bytes, the account-type byte, then TLV entries.</summary>
    private static byte[] Extended(byte accountType, params (ushort Type, byte[] Value)[] entries)
    {
        var data = new byte[166 + entries.Sum(entry => 4 + entry.Value.Length)];
        data[165] = accountType;

        var offset = 166;
        foreach (var (type, value) in entries)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), type);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2), (ushort)value.Length);
            value.CopyTo(data.AsSpan(offset + 4));
            offset += 4 + value.Length;
        }

        return data;
    }

    // Mirrors spl_token_2022_interface's test_transfer_fee_config fixture: config authority [10;32],
    // withdraw authority [11;32], withheld u64::MAX, older {epoch 1, max 10, 100 bps},
    // newer {epoch 100, max 5000, 1 bps}.
    private static byte[] TransferFeeConfigValue()
    {
        var value = new byte[TransferFeeConfig.Length];
        Array.Fill(value, (byte)10, 0, 32);
        Array.Fill(value, (byte)11, 32, 32);
        BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(64), ulong.MaxValue);
        BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(72), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(80), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(88), 100);
        BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(90), 100);
        BinaryPrimitives.WriteUInt64LittleEndian(value.AsSpan(98), 5000);
        BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(106), 1);
        return value;
    }

    [TestFixture]
    public sealed class DecodeMint
    {
        [Test]
        public void TransferFeeAndPointerAndCloseAuthority_DecodeTyped()
        {
            // Arrange
            var pointer = new byte[MetadataPointer.Length];
            Array.Fill(pointer, (byte)7, 0, 32);
            Array.Fill(pointer, (byte)8, 32, 32);

            var data = Extended(
                accountType: 1,
                ((ushort)ExtensionType.TransferFeeConfig, TransferFeeConfigValue()),
                ((ushort)ExtensionType.MetadataPointer, pointer),
                ((ushort)ExtensionType.MintCloseAuthority, new byte[32]), // all-zero = no authority set
                ((ushort)ExtensionType.DefaultAccountState, [2]));

            // Act
            var extensions = TokenExtensionSet.DecodeMint(data);

            // Assert
            extensions.Should().NotBeNull();
            extensions.Extensions.Should().HaveCount(4);

            var fee = extensions.GetTransferFeeConfig();
            fee.Should().NotBeNull();
            fee.TransferFeeConfigAuthority.Should().Be(Pk(10));
            fee.WithdrawWithheldAuthority.Should().Be(Pk(11));
            fee.WithheldAmount.Should().Be(ulong.MaxValue);
            fee.OlderTransferFee.Should().Be(new TransferFee { Epoch = 1, MaximumFee = 10, BasisPoints = 100 });
            fee.NewerTransferFee.Should().Be(new TransferFee { Epoch = 100, MaximumFee = 5000, BasisPoints = 1 });
            fee.GetEpochFee(99).Should().Be(fee.OlderTransferFee);
            fee.GetEpochFee(100).Should().Be(fee.NewerTransferFee);

            var metadataPointer = extensions.GetMetadataPointer();
            metadataPointer.Should().NotBeNull();
            metadataPointer.Authority.Should().Be(Pk(7));
            metadataPointer.MetadataAddress.Should().Be(Pk(8));

            extensions.Has(ExtensionType.MintCloseAuthority).Should().BeTrue();
            extensions.GetMintCloseAuthority().Should().BeNull("an all-zero OptionalNonZeroPubkey means none");
            extensions.GetDefaultAccountState().Should().Be(TokenAccountState.Frozen);
        }

        [Test]
        public void BareMint_ReturnsEmptySet()
            => TokenExtensionSet.DecodeMint(new byte[Mint.Length])!.Extensions.Should().BeEmpty();

        [Test]
        public void WrongAccountTypeByte_ReturnsNull()
            => TokenExtensionSet.DecodeMint(Extended(accountType: 2)).Should().BeNull();

        [Test]
        public void TruncatedTlvValue_ReturnsNull()
        {
            // Arrange: the entry claims 64 value bytes but the buffer ends after 4.
            var data = Extended(accountType: 1, ((ushort)ExtensionType.MetadataPointer, new byte[4]));
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(168), 64);

            // Act & Assert
            TokenExtensionSet.DecodeMint(data).Should().BeNull();
        }

        [Test]
        public void ZeroPadding_EndsTheWalk()
        {
            // Arrange: one real entry followed by zero padding (an Uninitialized tag).
            var entry = Extended(accountType: 1, ((ushort)ExtensionType.ImmutableOwner, []));
            var padded = new byte[entry.Length + 8];
            entry.CopyTo(padded, 0);

            // Act
            var extensions = TokenExtensionSet.DecodeMint(padded);

            // Assert
            extensions!.Extensions.Should().ContainSingle().Which.Type.Should().Be(ExtensionType.ImmutableOwner);
        }

        [Test]
        public void SingleTrailingReallocationByte_IsAccepted()
        {
            byte[] data = [.. Extended(accountType: 1), 0xAA];

            TokenExtensionSet.DecodeMint(data).Should().NotBeNull();
        }

        [Test]
        public void UninitializedType_StopsWithoutInspectingRemainingBytes()
        {
            byte[] data = [.. Extended(accountType: 1), 0, 0, 0xAA, 0xBB];

            TokenExtensionSet.DecodeMint(data).Should().NotBeNull();
        }

        [Test]
        public void OverlongFixedSizeExtensions_ReturnNullFromTypedViews()
        {
            // Arrange: StateWithExtensions returns the TLV bytes, but its typed Pod view requires
            // each fixed-size extension's declared length to equal size_of::<V>() exactly.
            var data = Extended(
                accountType: 1,
                ((ushort)ExtensionType.TransferFeeConfig, new byte[TransferFeeConfig.Length + 1]),
                ((ushort)ExtensionType.MintCloseAuthority, new byte[PublicKey.Length + 1]),
                ((ushort)ExtensionType.PermanentDelegate, new byte[PublicKey.Length + 1]),
                ((ushort)ExtensionType.DefaultAccountState, new byte[2]),
                ((ushort)ExtensionType.MetadataPointer, new byte[MetadataPointer.Length + 1]));

            // Act
            var extensions = TokenExtensionSet.DecodeMint(data)!;

            // Assert
            extensions.GetTransferFeeConfig().Should().BeNull();
            extensions.GetMintCloseAuthority().Should().BeNull();
            extensions.GetPermanentDelegate().Should().BeNull();
            extensions.GetDefaultAccountState().Should().BeNull();
            extensions.GetMetadataPointer().Should().BeNull();
        }

        [TestCase((byte)3)]
        [TestCase(byte.MaxValue)]
        public void UndefinedDefaultAccountState_ReturnsNull(byte state)
        {
            var extensions = TokenExtensionSet.DecodeMint(
                Extended(accountType: 1, ((ushort)ExtensionType.DefaultAccountState, [state])));

            extensions.Should().NotBeNull();
            extensions!.GetDefaultAccountState().Should().BeNull();
        }

        [Test]
        public void UnknownFutureExtensionType_IsPreservedAsOpaqueData()
        {
            var extensions = TokenExtensionSet.DecodeMint(
                Extended(accountType: 1, (29, [0xAA, 0xBB])));

            extensions.Should().NotBeNull();
            extensions!.Extensions.Should().ContainSingle().Which.Should().BeEquivalentTo(
                new TokenExtension { Type = (ExtensionType)29, Data = [0xAA, 0xBB] });
        }

        [Test]
        public void TokenMetadata_DecodesBorshContent()
        {
            // Arrange: OptionalNonZeroPubkey (none) + Borsh mint, name, symbol, uri, and one extra pair.
            using var buffer = new MemoryStream();
            buffer.Write(new byte[32]);
            buffer.Write(Pk(3).ToBytes());
            WriteBorshString(buffer, "Cool Token");
            WriteBorshString(buffer, "COOL");
            WriteBorshString(buffer, "https://example.org/cool.json");
            Span<byte> count = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(count, 1);
            buffer.Write(count);
            WriteBorshString(buffer, "kind");
            WriteBorshString(buffer, "meme");

            var data = Extended(accountType: 1, ((ushort)ExtensionType.TokenMetadata, buffer.ToArray()));

            // Act
            var metadata = TokenExtensionSet.DecodeMint(data)!.GetTokenMetadata();

            // Assert
            metadata.Should().NotBeNull();
            metadata.UpdateAuthority.Should().BeNull();
            metadata.Mint.Should().Be(Pk(3));
            metadata.Name.Should().Be("Cool Token");
            metadata.Symbol.Should().Be("COOL");
            metadata.Uri.Should().Be("https://example.org/cool.json");
            metadata.AdditionalMetadata.Should().ContainSingle()
                .Which.Should().Be(new KeyValuePair<string, string>("kind", "meme"));
        }

        [Test]
        public void TokenMetadata_HostileAdditionalMetadataCount_ReturnsNullWithoutAllocatingFromCount()
        {
            // Arrange: valid fixed fields followed by an impossible Vec length and no pair data.
            using var buffer = new MemoryStream();
            buffer.Write(new byte[32]);
            buffer.Write(Pk(3).ToBytes());
            WriteBorshString(buffer, "name");
            WriteBorshString(buffer, "symbol");
            WriteBorshString(buffer, "uri");
            Span<byte> count = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(count, int.MaxValue);
            buffer.Write(count);

            var data = Extended(accountType: 1, ((ushort)ExtensionType.TokenMetadata, buffer.ToArray()));

            // Act
            var metadata = TokenExtensionSet.DecodeMint(data)!.GetTokenMetadata();

            // Assert
            metadata.Should().BeNull();
        }

        private static void WriteBorshString(MemoryStream buffer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)bytes.Length);
            buffer.Write(length);
            buffer.Write(bytes);
        }
    }

    [TestFixture]
    public sealed class DecodeAccount
    {
        [Test]
        public void AccountExtensions_DecodeTyped()
        {
            // Arrange
            var withheld = new byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(withheld, 777);

            var data = Extended(
                accountType: 2,
                ((ushort)ExtensionType.TransferFeeAmount, withheld),
                ((ushort)ExtensionType.MemoTransfer, [1]),
                ((ushort)ExtensionType.ImmutableOwner, []));

            // Act
            var extensions = TokenExtensionSet.DecodeAccount(data);

            // Assert
            extensions.Should().NotBeNull();
            extensions.GetWithheldTransferFee().Should().Be(777ul);
            extensions.GetMemoTransferRequired().Should().BeTrue();
            extensions.Has(ExtensionType.ImmutableOwner).Should().BeTrue();
            extensions.Find(ExtensionType.CpiGuard).Should().BeNull();
        }

        [Test]
        public void BareAccount_ReturnsEmptySet()
            => TokenExtensionSet.DecodeAccount(new byte[TokenAccount.Length])!.Extensions.Should().BeEmpty();

        [Test]
        public void TooShort_ReturnsNull()
            => TokenExtensionSet.DecodeAccount(new byte[10]).Should().BeNull();

        [Test]
        public void OverlongFixedSizeExtensions_ReturnNullFromTypedViews()
        {
            // Arrange
            var data = Extended(
                accountType: 2,
                ((ushort)ExtensionType.TransferFeeAmount, new byte[sizeof(ulong) + 1]),
                ((ushort)ExtensionType.MemoTransfer, new byte[2]));

            // Act
            var extensions = TokenExtensionSet.DecodeAccount(data)!;

            // Assert
            extensions.GetWithheldTransferFee().Should().BeNull();
            extensions.GetMemoTransferRequired().Should().BeNull();
        }

        [Test]
        public void MemoTransferPodBool_NonZeroValueReturnsTrue()
        {
            var extensions = TokenExtensionSet.DecodeAccount(
                Extended(accountType: 2, ((ushort)ExtensionType.MemoTransfer, [byte.MaxValue])));

            extensions!.GetMemoTransferRequired().Should().BeTrue();
        }
    }
}
