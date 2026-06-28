using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class ComputeBudgetProgramTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    [TestFixture]
    public sealed class SetComputeUnitLimit
    {
        // Reference bytes from solders: set_compute_unit_limit(200_000).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instruction = ComputeBudgetProgram.SetComputeUnitLimit(200_000);

            // Assert
            instruction.ProgramId.Should().Be(PublicKey.Parse(SolanaProgramIds.ComputeBudgetProgram));
            instruction.Accounts.Should().BeEmpty();
            instruction.Data.Should().Equal(Hex("02400d0300"));
        }
    }

    [TestFixture]
    public sealed class SetComputeUnitPrice
    {
        // Reference bytes from solders: set_compute_unit_price(1000).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instruction = ComputeBudgetProgram.SetComputeUnitPrice(1000);

            // Assert
            instruction.ProgramId.Should().Be(PublicKey.Parse(SolanaProgramIds.ComputeBudgetProgram));
            instruction.Accounts.Should().BeEmpty();
            instruction.Data.Should().Equal(Hex("03e803000000000000"));
        }
    }

    [TestFixture]
    public sealed class SetPriorityFee
    {
        [Test]
        public void ReturnsLimitThenPriceInstructions()
        {
            // Act
            var instructions = ComputeBudgetProgram.SetPriorityFee(200_000, 1000);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Data.Should().Equal(Hex("02400d0300"));        // SetComputeUnitLimit(200000)
            instructions[1].Data.Should().Equal(Hex("03e803000000000000")); // SetComputeUnitPrice(1000)
        }
    }
}
