using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Programs.Tests;

public static class ElGamalProofProgramDirectCoverageTests
{
    [TestFixture]
    public sealed class TryDecodeInstruction
    {
        [Test]
        public void DefinedDiscriminatorsAndBoundaries_AreDecodedExactly()
        {
            // Arrange
            var defined = Enum.GetValues<ElGamalProofInstruction>();

            // Act & Assert
            foreach (var expected in defined)
            {
                ElGamalProofProgram.TryDecodeInstruction([(byte)expected, 0xff], out var actual)
                    .Should().BeTrue();
                actual.Should().Be(expected);
            }

            ElGamalProofProgram.TryDecodeInstruction([], out var empty).Should().BeFalse();
            empty.Should().Be(default);
            ElGamalProofProgram.TryDecodeInstruction([13], out var next).Should().BeFalse();
            next.Should().Be(default);
            ElGamalProofProgram.TryDecodeInstruction([byte.MaxValue], out var maximum).Should().BeFalse();
            maximum.Should().Be(default);
        }
    }

    [TestFixture]
    public sealed class GetProofDataLength
    {
        [Test]
        public void VerifierVariants_ReturnPinnedPodLengths()
        {
            // Arrange
            (ElGamalProofInstruction Instruction, int Length)[] expected =
            [
                (ElGamalProofInstruction.VerifyZeroCiphertext, 192),
                (ElGamalProofInstruction.VerifyCiphertextCiphertextEquality, 416),
                (ElGamalProofInstruction.VerifyCiphertextCommitmentEquality, 320),
                (ElGamalProofInstruction.VerifyPubkeyValidity, 96),
                (ElGamalProofInstruction.VerifyPercentageWithCap, 360),
                (ElGamalProofInstruction.VerifyBatchedRangeProofU64, 936),
                (ElGamalProofInstruction.VerifyBatchedRangeProofU128, 1000),
                (ElGamalProofInstruction.VerifyBatchedRangeProofU256, 1064),
                (ElGamalProofInstruction.VerifyGroupedCiphertext2HandlesValidity, 320),
                (ElGamalProofInstruction.VerifyBatchedGroupedCiphertext2HandlesValidity, 416),
                (ElGamalProofInstruction.VerifyGroupedCiphertext3HandlesValidity, 416),
                (ElGamalProofInstruction.VerifyBatchedGroupedCiphertext3HandlesValidity, 544)
            ];

            // Act & Assert
            foreach (var (instruction, length) in expected)
                ElGamalProofProgram.GetProofDataLength(instruction).Should().Be(length);
        }

        [Test]
        public void CloseAndUnknownDiscriminators_AreRejected()
        {
            // Act
            Action close = () => _ = ElGamalProofProgram.GetProofDataLength(
                ElGamalProofInstruction.CloseContextState);
            Action unknown = () => _ = ElGamalProofProgram.GetProofDataLength((ElGamalProofInstruction)13);

            // Assert
            close.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("proofInstruction");
            unknown.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("proofInstruction");
        }
    }
}
