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
            Action act = () => SolanaUnits.SolToLamports(-1m);
            act.Should().Throw<ArgumentOutOfRangeException>();
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
