using System.Buffers.Binary;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.TokenDecoderTestHelpers;

namespace SolSharp.Programs.Tests;

public static class TokenDecoderTests
{
    [TestFixture]
    public sealed class DecodeInstructionData
    {
        [Test]
        public void ClassicFixedAndOptionalFields_AreDecoded()
        {
            // Arrange
            var initialize = TokenProgram.InitializeMint(Key(1), 6, Key(2), Key(3));
            var setAuthority = TokenProgram.SetAuthority(Key(1), Key(2), AuthorityType.CloseAccount, null);
            var checkedTransfer = TokenProgram.TransferChecked(Key(1), Key(2), Key(3), Key(4), 500, 6);

            // Act
            var decodedInitialize = TokenProgram.DecodeInstructionData(initialize.Data);
            var decodedAuthority = TokenProgram.DecodeInstructionData(setAuthority.Data);
            var decodedTransfer = TokenProgram.DecodeInstructionData(checkedTransfer.Data);

            // Assert
            decodedInitialize.Should().NotBeNull();
            decodedInitialize!.Discriminator.Should().Be(initialize.Data[0]);
            decodedInitialize.Payload.ToArray().Should().Equal(initialize.Data[1..]);
            decodedInitialize.Name.Should().Be("InitializeMint");
            decodedInitialize.Decimals.Should().Be(6);
            decodedInitialize.RelatedPublicKey.Should().Be(Key(2));
            decodedInitialize.OptionalPublicKey.Should().Be(Key(3));
            decodedAuthority.Should().NotBeNull();
            decodedAuthority!.AuthorityType.Should().Be(AuthorityType.CloseAccount);
            decodedAuthority.HasOptionalPublicKey.Should().BeTrue();
            decodedAuthority.OptionalPublicKey.Should().BeNull();
            decodedTransfer.Should().NotBeNull();
            decodedTransfer!.Amount.Should().Be(500);
            decodedTransfer.Decimals.Should().Be(6);
        }

        [Test]
        public void ExtensionsDeprecatedStableVariantsAndBatch_AreDecoded()
        {
            // Arrange
            var confidential = Token2022Program.EnableConfidentialCredits(Key(1), Key(2));
            var unwrap = TokenProgram.UnwrapLamports(Key(1), Key(2), Key(3), 100);
            var accountDataSize = Token2022Program.GetAccountDataSize(
                Key(1), [Token2022ExtensionType.TransferFeeConfig, Token2022ExtensionType.MemoTransfer]);
            var batch = TokenProgram.Batch(
                [
                    TokenProgram.SyncNative(Key(1), Token2022Program.ProgramId),
                    TokenProgram.Transfer(Key(1), Key(2), Key(3), 4, Token2022Program.ProgramId)
                ]);

            // Act
            var decodedConfidential = TokenProgram.DecodeInstructionData(confidential.Data);
            var decodedUnwrap = TokenProgram.DecodeInstructionData(unwrap.Data);
            var decodedAccountDataSize = TokenProgram.DecodeInstructionData(accountDataSize.Data);
            var decodedBatch = TokenProgram.DecodeInstructionData(batch.Data);

            // Assert
            decodedConfidential.Should().NotBeNull();
            decodedConfidential!.Name.Should().Be("ConfidentialTransferExtension");
            decodedConfidential.ExtensionInstructionDiscriminator.Should().Be(9);
            decodedUnwrap.Should().NotBeNull();
            decodedUnwrap!.HasOptionalAmount.Should().BeTrue();
            decodedUnwrap.Amount.Should().Be(100);
            decodedAccountDataSize.Should().NotBeNull();
            decodedAccountDataSize!.Discriminator.Should().Be(21);
            decodedAccountDataSize.Payload.ToArray().Should().Equal(accountDataSize.Data[1..]);
            decodedAccountDataSize.ExtensionTypes.Should().Equal(
                Token2022ExtensionType.TransferFeeConfig,
                Token2022ExtensionType.MemoTransfer);
            TokenProgram.DecodeInstructionData([255]).Should().BeNull();
            TokenProgram.DecodeInstructionData([255, 0, 0]).Should().BeNull();
            decodedBatch.Should().NotBeNull();
            decodedBatch!.BatchEntries.Select(entry => (entry.AccountCount, entry.Data.ToArray())).Should().SatisfyRespectively(
                first =>
                {
                    first.AccountCount.Should().Be(1);
                    first.Item2.Should().Equal(17);
                },
                second =>
                {
                    second.AccountCount.Should().Be(3);
                    second.Item2.Should().Equal(TokenProgram.Transfer(Key(1), Key(2), Key(3), 4).Data);
                });
        }

        [Test]
        public void MalformedPinnedLayouts_AreRejected()
        {
            // Act & Assert
            TokenProgram.DecodeInstructionData([]).Should().BeNull();
            TokenProgram.DecodeInstructionData([12, 1]).Should().BeNull();
            TokenProgram.DecodeInstructionData([6, 255, 0]).Should().BeNull();
            TokenProgram.DecodeInstructionData([29, 1]).Should().BeNull();
            TokenProgram.DecodeInstructionData([255, 1, 3, 17]).Should().BeNull();
        }

        [Test]
        public void InterfaceDiscriminatorDecoders_MatchBuilders()
        {
            // Arrange
            var hook = TransferHookProgram.Execute(Key(9), Key(1), Key(2), Key(3), Key(4), 5);

            // Act
            var decodedHook = TransferHookProgram.DecodeInstructionData(hook.Data);
            var decodedAssociatedToken = AssociatedTokenAccount.DecodeInstructionData([1]);
            var malformedAssociatedToken = AssociatedTokenAccount.DecodeInstructionData([1, 0]);

            // Assert
            decodedHook!.Name.Should().Be("Execute");
            decodedAssociatedToken.Should().Be("CreateIdempotent");
            malformedAssociatedToken.Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class DecodeTokenMetadataInstructionData
    {
        [Test]
        public void InterfaceDiscriminator_MatchesBuilder()
        {
            // Arrange
            var metadata = Token2022Program.InitializeTokenMetadata(
                Key(1), Key(2), Key(3), Key(4), "name", "SYM", "uri");

            // Act
            var decoded = Token2022Program.DecodeTokenMetadataInstructionData(metadata.Data);

            // Assert
            decoded!.Name.Should().Be("Initialize");
            decoded.Payload.ToArray().Should().Equal(metadata.Data[8..]);
        }
    }

    [TestFixture]
    public sealed class DecodeTokenGroupInstructionData
    {
        [Test]
        public void InterfaceDiscriminator_MatchesBuilder()
        {
            // Arrange
            var group = Token2022Program.UpdateTokenGroupMaxSize(Key(1), Key(2), 3);

            // Act
            var decoded = Token2022Program.DecodeTokenGroupInstructionData(group.Data);

            // Assert
            decoded!.Name.Should().Be("UpdateGroupMaxSize");
            decoded.Payload.ToArray().Should().Equal(group.Data[8..]);
        }
    }
}

public static class TokenMintStateDecoderTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void ClassicAndExtendedState_IsDecoded()
        {
            // Arrange
            var mintData = MintData();
            var extendedData = new byte[166 + 4 + 3];
            mintData.CopyTo(extendedData, 0);
            extendedData[165] = 1;
            BinaryPrimitives.WriteUInt16LittleEndian(
                extendedData.AsSpan(166), (ushort)Token2022ExtensionType.ScaledUiAmount);
            BinaryPrimitives.WriteUInt16LittleEndian(extendedData.AsSpan(168), 3);
            extendedData.AsSpan(170).Fill(9);

            // Act
            var classic = TokenMintState.Decode(mintData);
            var extended = TokenMintState.Decode(extendedData);

            // Assert
            classic.Should().NotBeNull();
            classic!.MintAuthority.Should().Be(Key(1));
            classic.Supply.Should().Be(500);
            classic.Decimals.Should().Be(6);
            classic.IsInitialized.Should().BeTrue();
            classic.FreezeAuthority.Should().BeNull();
            classic.Extensions.Should().BeEmpty();
            extended.Should().NotBeNull();
            extended!.Extensions.Should().ContainSingle();
            extended.Extensions[0].ExtensionType.Should().Be(Token2022ExtensionType.ScaledUiAmount);
            extended.Extensions[0].Data.ToArray().Should().Equal("\t\t\t"u8.ToArray());
        }

        [Test]
        public void MultisigReservedLength_IsRejected()
        {
            // Arrange
            var data = ExtendedMintData(TokenMultisigState.Length);

            // Act & Assert
            TokenMintState.Decode(data).Should().BeNull();
        }

        [TestCase(81)]
        [TestCase(83)]
        [TestCase(164)]
        [TestCase(165)]
        public void IntermediateEnvelopeLengths_AreRejected(int length)
        {
            // Arrange
            var data = new byte[length];
            MintData().AsSpan(0, Math.Min(length, TokenMintState.BaseLength)).CopyTo(data);

            // Act & Assert
            TokenMintState.Decode(data).Should().BeNull();
        }

        [TestCase(354)]
        [TestCase(356)]
        [TestCase(357)]
        public void NeighboringExtendedLengths_AreNotMistakenForMultisig(int length)
        {
            // Arrange
            var data = ExtendedMintData(length);

            // Act
            var state = TokenMintState.Decode(data);

            // Assert
            state.Should().NotBeNull();
            state!.Extensions.Should().BeEmpty();
        }

        [Test]
        public void TruncatedClassicState_IsRejected() =>
            // Act & Assert
            TokenMintState.Decode(MintData().AsSpan()[..^1]).Should().BeNull();
    }
}

public static class TokenHoldingAccountStateDecoderTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void ClassicAndExtendedState_IsDecoded()
        {
            // Arrange
            var data = new byte[166 + 4 + 1];
            Key(1).CopyTo(data);
            Key(2).CopyTo(data.AsSpan(32));
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(64), 100);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(72), 1);
            Key(3).CopyTo(data.AsSpan(76));
            data[108] = (byte)TokenAccountStatus.Frozen;
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(109), 1);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(113), 2_039_280);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(121), 25);
            data[165] = 2;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(166), (ushort)Token2022ExtensionType.MemoTransfer);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(168), 1);
            data[170] = 1;

            // Act
            var state = TokenHoldingAccountState.Decode(data);

            // Assert
            state.Should().NotBeNull();
            state!.Mint.Should().Be(Key(1));
            state.Owner.Should().Be(Key(2));
            state.Amount.Should().Be(100);
            state.Delegate.Should().Be(Key(3));
            state.Status.Should().Be(TokenAccountStatus.Frozen);
            state.NativeRentExemptReserve.Should().Be(2_039_280);
            state.DelegatedAmount.Should().Be(25);
            state.Extensions.Should().ContainSingle();
        }

        [Test]
        public void MultisigReservedLength_IsRejected()
        {
            // Arrange
            var data = ExtendedHoldingData(TokenMultisigState.Length);

            // Act & Assert
            TokenHoldingAccountState.Decode(data).Should().BeNull();
        }

        [TestCase(164)]
        public void IntermediateEnvelopeLengths_AreRejected(int length)
        {
            // Arrange
            var data = new byte[length];

            // Act & Assert
            TokenHoldingAccountState.Decode(data).Should().BeNull();
        }

        [TestCase(354)]
        [TestCase(356)]
        [TestCase(357)]
        public void NeighboringExtendedLengths_AreNotMistakenForMultisig(int length)
        {
            // Arrange
            var data = ExtendedHoldingData(length);

            // Act
            var state = TokenHoldingAccountState.Decode(data);

            // Assert
            state.Should().NotBeNull();
            state!.Extensions.Should().BeEmpty();
        }
    }
}

public static class TokenMultisigStateDecoderTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void ValidState_IsDecodedAndMalformedFlagIsRejected()
        {
            // Arrange
            var data = new byte[TokenMultisigState.Length];
            data[0] = 2;
            data[1] = 3;
            data[2] = 1;
            for (var i = 0; i < 11; i++)
                Key(checked((byte)(i + 1))).CopyTo(data.AsSpan(3 + (i * PublicKey.Length)));

            // Act
            var state = TokenMultisigState.Decode(data);

            // Assert
            state.Should().NotBeNull();
            state!.RequiredSignatures.Should().Be(2);
            state.SignerCount.Should().Be(3);
            state.Signers.Take(3).Should().Equal(Key(1), Key(2), Key(3));
            data[2] = 2;
            TokenMultisigState.Decode(data).Should().BeNull();
        }
    }
}

public static class TokenMetadataStateDecoderTests
{
    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void PinnedBorshValue_IsDecoded()
        {
            // Arrange
            var data = new List<byte>();
            data.AddRange(Key(1).ToBytes());
            data.AddRange(Key(2).ToBytes());
            WriteString(data, "Name");
            WriteString(data, "SYM");
            WriteString(data, "https://example.test");
            WriteUInt32(data, 1);
            WriteString(data, "kind");
            WriteString(data, "test");

            // Act
            var state = TokenMetadataState.Decode(data.ToArray());

            // Assert
            state.Should().NotBeNull();
            state!.UpdateAuthority.Should().Be(Key(1));
            state.Mint.Should().Be(Key(2));
            state.Name.Should().Be("Name");
            state.Symbol.Should().Be("SYM");
            state.Uri.Should().Be("https://example.test");
            state.AdditionalMetadata.Should().ContainSingle();
            state.AdditionalMetadata[0].Key.Should().Be("kind");
            state.AdditionalMetadata[0].Value.Should().Be("test");
        }

        [Test]
        public void HugeAdditionalCount_IsRejectedBeforeAllocation()
        {
            // Arrange: two keys, three empty strings, then an impossible untrusted vector count.
            var data = new byte[(PublicKey.Length * 2) + (sizeof(uint) * 4)];
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(data.Length - sizeof(uint)), uint.MaxValue);

            // Act & Assert
            TokenMetadataState.Decode(data).Should().BeNull();
        }
    }
}

internal static class TokenDecoderTestHelpers
{
    internal static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    internal static byte[] MintData()
    {
        var data = new byte[TokenMintState.BaseLength];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 1);
        Key(1).CopyTo(data.AsSpan(4));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(36), 500);
        data[44] = 6;
        data[45] = 1;
        return data;
    }

    internal static byte[] ExtendedMintData(int length)
    {
        var data = new byte[length];
        MintData().CopyTo(data, 0);
        data[165] = 1;
        return data;
    }

    internal static byte[] ExtendedHoldingData(int length)
    {
        var data = new byte[length];
        Key(1).CopyTo(data);
        Key(2).CopyTo(data.AsSpan(PublicKey.Length));
        data[108] = (byte)TokenAccountStatus.Initialized;
        data[165] = 2;
        return data;
    }

    internal static void WriteString(List<byte> data, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteUInt32(data, checked((uint)bytes.Length));
        data.AddRange(bytes);
    }

    internal static void WriteUInt32(List<byte> data, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        data.AddRange(bytes.ToArray());
    }
}
