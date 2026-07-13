using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Core.Tests;

public static class SolanaUnitsTests
{
    [TestFixture]
    public sealed class SolToLamports
    {
        [Test]
        public void ConvertsWholeAndFractionalSol()
        {
            SolanaUnits.SolToLamports(1m).Should().Be(1_000_000_000ul);
            SolanaUnits.SolToLamports(1.5m).Should().Be(1_500_000_000ul);
            SolanaUnits.SolToLamports(0.000000001m).Should().Be(1ul);
        }

        [Test]
        public void NegativeThrows()
        {
            // Act
            Action act = () => SolanaUnits.SolToLamports(-1m);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void LargerThanUlongLamports_Throws()
        {
            // Act: 18_446_744_074 SOL converts past ulong.MaxValue lamports.
            Action act = () => SolanaUnits.SolToLamports(18_446_744_074m);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void FarPastDecimalMultiplyRange_StillThrowsArgumentOutOfRange()
        {
            // Act: 1e20 SOL used to overflow the decimal multiply before the range check ran,
            // surfacing as OverflowException instead of the documented exception.
            Action act = () => SolanaUnits.SolToLamports(1e20m);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Test]
        public void ExactUlongBoundary_Converts()
        {
            // 18_446_744_073.709551615 SOL is exactly ulong.MaxValue lamports.
            SolanaUnits.SolToLamports(18_446_744_073.709551615m).Should().Be(ulong.MaxValue);
        }
    }

    [TestFixture]
    public sealed class LamportsToSol
    {
        [Test]
        public void ConvertsAndRoundTrips()
        {
            SolanaUnits.LamportsToSol(1_000_000_000ul).Should().Be(1m);
            SolanaUnits.LamportsToSol(1ul).Should().Be(0.000000001m);
            SolanaUnits.SolToLamports(SolanaUnits.LamportsToSol(2_500_000_000ul)).Should().Be(2_500_000_000ul);
        }
    }
}
