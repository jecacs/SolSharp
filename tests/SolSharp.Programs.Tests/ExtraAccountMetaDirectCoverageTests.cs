using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class ExtraAccountSeedDirectCoverageTests
{
    [TestFixture]
    public sealed class DecodeConfiguration
    {
        [Test]
        public void MixedPinnedSeedLayout_DecodesAllPublicFields()
        {
            // Arrange
            var configuration = new byte[ExtraAccountMeta.AddressConfigurationLength];
            new byte[] { 1, 2, (byte)'a', (byte)'b', 2, 8, 4, 3, 5, 4, 6, 7, 8 }
                .CopyTo(configuration, 0);

            // Act
            var seeds = ExtraAccountSeed.DecodeConfiguration(configuration);

            // Assert
            seeds.Should().HaveCount(4);
            seeds![0].Kind.Should().Be(ExtraAccountSeedKind.Literal);
            seeds[0].LiteralBytes.ToArray().Should().Equal("ab"u8.ToArray());
            seeds[1].Kind.Should().Be(ExtraAccountSeedKind.InstructionData);
            seeds[1].Index.Should().Be(8);
            seeds[1].Length.Should().Be(4);
            seeds[2].Kind.Should().Be(ExtraAccountSeedKind.AccountKey);
            seeds[2].Index.Should().Be(5);
            seeds[3].Kind.Should().Be(ExtraAccountSeedKind.AccountData);
            seeds[3].AccountIndex.Should().Be(6);
            seeds[3].DataIndex.Should().Be(7);
            seeds[3].Length.Should().Be(8);
        }

        [Test]
        public void WrongLengthUnknownTagOrOverrun_IsRejected()
        {
            // Arrange
            var unknownTag = new byte[ExtraAccountMeta.AddressConfigurationLength];
            unknownTag[0] = 5;
            var literalOverrun = new byte[ExtraAccountMeta.AddressConfigurationLength];
            literalOverrun[0] = 1;
            literalOverrun[1] = 31;

            // Act & Assert
            ExtraAccountSeed.DecodeConfiguration(new byte[ExtraAccountMeta.AddressConfigurationLength - 1])
                .Should().BeNull();
            ExtraAccountSeed.DecodeConfiguration(unknownTag).Should().BeNull();
            ExtraAccountSeed.DecodeConfiguration(literalOverrun).Should().BeNull();
        }
    }
}

public static class ExtraAccountMetaDirectCoverageTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class FromExternalProgramDerivedAddress
    {
        [Test]
        public void PinnedExternalProgramLayout_ExposesConfigurationAndPrivileges()
        {
            // Arrange
            var seeds = new[]
            {
                ExtraAccountSeed.Literal("ab"u8),
                ExtraAccountSeed.FromAccountData(1, 3, 4)
            };
            var expectedConfiguration = new byte[ExtraAccountMeta.AddressConfigurationLength];
            new byte[] { 1, 2, (byte)'a', (byte)'b', 4, 1, 3, 4 }
                .CopyTo(expectedConfiguration, 0);

            // Act
            var meta = ExtraAccountMeta.FromExternalProgramDerivedAddress(
                7, seeds, isSigner: true, isWritable: false);
            var decodedSeeds = meta.DecodeSeeds();

            // Assert
            meta.Discriminator.Should().Be(0x87);
            meta.AddressConfiguration.ToArray().Should().Equal(expectedConfiguration);
            meta.IsSigner.Should().BeTrue();
            meta.IsWritable.Should().BeFalse();
            meta.Encode().Should().Equal([0x87, .. expectedConfiguration, 1, 0]);
            decodedSeeds.Should().HaveCount(2);
            decodedSeeds![0].LiteralBytes.ToArray().Should().Equal("ab"u8.ToArray());
            decodedSeeds[1].AccountIndex.Should().Be(1);
            decodedSeeds[1].DataIndex.Should().Be(3);
            decodedSeeds[1].Length.Should().Be(4);
        }

        [Test]
        public void ProgramIndexBoundary_UsesHighBitWithoutOverflow()
        {
            // Act
            var maximum = ExtraAccountMeta.FromExternalProgramDerivedAddress(
                127, [], isSigner: false, isWritable: false);
            Action beyondMaximum = () => _ = ExtraAccountMeta.FromExternalProgramDerivedAddress(
                128, [], isSigner: false, isWritable: false);

            // Assert
            maximum.Discriminator.Should().Be(byte.MaxValue);
            beyondMaximum.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("programIndex");
        }
    }

    [TestFixture]
    public sealed class TryGetPublicKey
    {
        [Test]
        public void FixedAndDerivedEntries_AreDistinguishedExactly()
        {
            // Arrange
            var fixedEntry = ExtraAccountMeta.FromPublicKey(Key(6), isSigner: false, isWritable: true);
            var derivedEntry = ExtraAccountMeta.FromProgramDerivedAddress(
                [ExtraAccountSeed.FromAccountKey(0)], isSigner: false, isWritable: false);

            // Act
            var fixedResult = fixedEntry.TryGetPublicKey(out var publicKey);
            var derivedResult = derivedEntry.TryGetPublicKey(out var missingPublicKey);

            // Assert
            fixedResult.Should().BeTrue();
            publicKey.Should().Be(Key(6));
            derivedResult.Should().BeFalse();
            missingPublicKey.Should().Be(default(PublicKey));
        }
    }
}
