using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class ProgramDerivedAddressTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

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
    }
}
