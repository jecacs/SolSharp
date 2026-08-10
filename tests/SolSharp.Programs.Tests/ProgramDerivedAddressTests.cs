using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class ProgramDerivedAddressTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    [TestFixture]
    public sealed class CreateWithSeed
    {
        [Test]
        public void UpstreamKnownVector_MatchesSolanaSdk()
        {
            // Act
            var address = ProgramDerivedAddress.CreateWithSeed(
                default,
                "limber chicken: 4/45",
                default);

            // Assert
            address.Should().Be(PublicKey.Parse("9h1HyLCW5dZnBVap8C5egQ9Z6pHyjsh5MNy83iPqqRuq"));
        }

        [Test]
        public void SeedLimit_IsMeasuredInUtf8Bytes()
        {
            // Arrange: each U+10FFFF scalar is four UTF-8 bytes, matching solana-sdk's boundary vector.
            var maxLengthSeed = string.Concat(Enumerable.Repeat("\U0010FFFF", 8));

            // Act
            Action accepted = () => ProgramDerivedAddress.CreateWithSeed(Key(1), maxLengthSeed, Key(2));
            Action rejected = () => ProgramDerivedAddress.CreateWithSeed(Key(1), "x" + maxLengthSeed, Key(2));

            // Assert
            accepted.Should().NotThrow();
            rejected.Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("seed");
        }

        [Test]
        public void OwnerEndingInReservedMarker_Throws()
        {
            // Arrange
            var ownerBytes = new byte[PublicKey.Length];
            "ProgramDerivedAddress"u8.CopyTo(ownerBytes.AsSpan(PublicKey.Length - 21));
            var owner = new PublicKey(ownerBytes);

            // Act
            Action act = () => ProgramDerivedAddress.CreateWithSeed(Key(1), "seed", owner);

            // Assert
            act.Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("owner");
        }

        [Test]
        public void NullSeed_Throws()
        {
            // Act
            Action act = static () => ProgramDerivedAddress.CreateWithSeed(Key(1), null!, Key(2));

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("seed");
        }

        [Test]
        public void InvalidUtf16Seed_Throws()
        {
            // Act
            Action act = static () => ProgramDerivedAddress.CreateWithSeed(Key(1), "\uD800", Key(2));

            // Assert
            act.Should().Throw<ArgumentException>()
                .Which.ParamName.Should().Be("seed");
        }
    }

    [TestFixture]
    public sealed class FindProgramAddress
    {
        // Reference from solders: find_program_address([b"hello"], program=[9;32]).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Arrange
            byte[][] seeds = [[0x68, 0x65, 0x6c, 0x6c, 0x6f]]; // "hello"

            // Act
            var (address, bump) = ProgramDerivedAddress.FindProgramAddress(seeds, Key(9));

            // Assert
            address.Should().Be(PublicKey.Parse("DPrMg7Y6Dp1XHiQjEwEEyDbAbA71jX7Z6L1ycmu1thcF"));
            bump.Should().Be(254);
        }

        [Test]
        public void SixteenSeeds_Throws()
        {
            // Arrange: 16 caller seeds leave no slot for the bump.
            var seeds = Enumerable.Range(0, ProgramDerivedAddress.MaxSeeds).Select(static i => new[] { (byte)i }).ToArray();

            // Act
            Action act = () => ProgramDerivedAddress.FindProgramAddress(seeds, Key(9));

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void NullSeed_ThrowsDocumentedArgumentNullException()
        {
            // Arrange
            byte[][] seeds = [null!];

            // Act
            Action act = () => ProgramDerivedAddress.FindProgramAddress(seeds, Key(9));

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be(nameof(seeds));
        }
    }

    [TestFixture]
    public sealed class TryCreateProgramAddress
    {
        [Test]
        public void SeedLongerThanMax_Throws()
        {
            // Arrange
            byte[][] seeds = [new byte[ProgramDerivedAddress.MaxSeedLength + 1]];

            // Act
            Action act = () => ProgramDerivedAddress.TryCreateProgramAddress(seeds, Key(9), out _);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void MoreSeedsThanMax_Throws()
        {
            // Arrange
            var seeds = Enumerable.Range(0, ProgramDerivedAddress.MaxSeeds + 1).Select(static i => new[] { (byte)i }).ToArray();

            // Act
            Action act = () => ProgramDerivedAddress.TryCreateProgramAddress(seeds, Key(9), out _);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void ExactlyMaxSeeds_IsAccepted()
        {
            // Arrange: 16 seeds is the solana-sdk limit itself, so the derivation must run (whether or not
            // the resulting hash lands off-curve).
            var seeds = Enumerable.Range(0, ProgramDerivedAddress.MaxSeeds).Select(static i => new[] { (byte)i }).ToArray();

            // Act
            Action act = () => ProgramDerivedAddress.TryCreateProgramAddress(seeds, Key(9), out _);

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void NullSeed_ThrowsDocumentedArgumentNullException()
        {
            // Arrange
            byte[][] seeds = [null!];

            // Act
            Action act = () => ProgramDerivedAddress.TryCreateProgramAddress(seeds, Key(9), out _);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be(nameof(seeds));
        }
    }
}
