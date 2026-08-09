using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class ConfidentialProofLocationTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class AtInstructionOffset
    {
        [Test]
        public void SignedBoundaries_ExposeOffsetBranchAndRejectZero()
        {
            // Act
            var minimum = ConfidentialProofLocation.AtInstructionOffset(sbyte.MinValue);
            var maximum = ConfidentialProofLocation.AtInstructionOffset(sbyte.MaxValue);
            Action zero = () => _ = ConfidentialProofLocation.AtInstructionOffset(0);

            // Assert
            minimum.IsInstructionOffset.Should().BeTrue();
            minimum.InstructionOffset.Should().Be(sbyte.MinValue);
            minimum.ContextStateAccount.Should().BeNull();
            maximum.IsInstructionOffset.Should().BeTrue();
            maximum.InstructionOffset.Should().Be(sbyte.MaxValue);
            maximum.ContextStateAccount.Should().BeNull();
            zero.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("instructionOffset");
        }
    }

    [TestFixture]
    public sealed class AtContextState
    {
        [Test]
        public void ContextBranch_ExposesZeroOffsetAndAccount()
        {
            // Arrange
            var context = Key(9);

            // Act
            var location = ConfidentialProofLocation.AtContextState(context);

            // Assert
            location.IsInstructionOffset.Should().BeFalse();
            location.InstructionOffset.Should().Be(0);
            location.ContextStateAccount.Should().Be(context);
        }
    }
}
