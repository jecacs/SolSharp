using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class FeatureGateProgramTests
{
    private static readonly PublicKey Feature = new(Enumerable.Repeat((byte)1, 32).ToArray());
    private static readonly PublicKey Funder = new(Enumerable.Repeat((byte)2, 32).ToArray());

    private static void AssertMeta(AccountMeta meta, PublicKey key, bool isSigner, bool isWritable)
    {
        meta.PublicKey.Should().Be(key);
        meta.IsSigner.Should().Be(isSigner);
        meta.IsWritable.Should().Be(isWritable);
    }

    [TestFixture]
    public sealed class ActivateWithLamports
    {
        [Test]
        public void PinnedCompositeVector_MatchesTransferAllocateAssignSequence()
        {
            // Arrange
            const ulong lamports = 0x0807060504030201;

            // Act
            var instructions = FeatureGateProgram.ActivateWithLamports(Feature, Funder, lamports);

            // Assert
            instructions.Should().HaveCount(3);
            instructions.Select(instruction => instruction.ProgramId).Should().OnlyContain(id => id == SystemProgram.ProgramId);
            instructions.Select(instruction => Convert.ToHexString(instruction.Data)).Should().Equal(
                "020000000102030405060708",
                "080000000900000000000000",
                "01000000" + Convert.ToHexString(FeatureGateProgram.ProgramId.ToBytes()));
            AssertMeta(instructions[0].Accounts[0], Funder, isSigner: true, isWritable: true);
            AssertMeta(instructions[0].Accounts[1], Feature, isSigner: false, isWritable: true);
            AssertMeta(instructions[1].Accounts.Single(), Feature, isSigner: true, isWritable: true);
            AssertMeta(instructions[2].Accounts.Single(), Feature, isSigner: true, isWritable: true);
            FeatureGateProgram.ProgramId.ToString().Should().Be("Feature111111111111111111111111111111111111");
        }
    }

    [TestFixture]
    public sealed class RevokePendingActivation
    {
        [Test]
        public void PinnedCompositeVector_MatchesProgramDataAndAccountMetas()
        {
            // Act
            var instruction = FeatureGateProgram.RevokePendingActivation(Feature);

            // Assert
            instruction.ProgramId.Should().Be(FeatureGateProgram.ProgramId);
            instruction.Data.Should().Equal(0);
            instruction.Accounts.Should().HaveCount(3);
            AssertMeta(instruction.Accounts[0], Feature, isSigner: true, isWritable: true);
            AssertMeta(instruction.Accounts[1], FeatureGateProgram.IncineratorId, isSigner: false, isWritable: true);
            AssertMeta(instruction.Accounts[2], SystemProgram.ProgramId, isSigner: false, isWritable: false);
            FeatureGateProgram.IncineratorId.ToString().Should()
                .Be("1nc1nerator11111111111111111111111111111111");
        }
    }
}
