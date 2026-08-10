using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class InstructionsSysvarTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void TwoInstructions_MatchPinnedRustLayoutAndRoundTrip()
        {
            // Arrange
            var first = new Instruction
            {
                ProgramId = Key(2),
                Accounts = [AccountMeta.WritableSigner(Key(1))],
                Data = [0xAA, 0xBB]
            };
            var second = new Instruction
            {
                ProgramId = Key(3),
                Accounts = [],
                Data = [0xCC]
            };
            var expected = Convert.FromHexString(
                "020006004d00" +
                "010003" + string.Concat(Enumerable.Repeat("01", PublicKey.Length)) +
                string.Concat(Enumerable.Repeat("02", PublicKey.Length)) + "0200aabb" +
                "0000" + string.Concat(Enumerable.Repeat("03", PublicKey.Length)) + "0100cc" +
                "0100");

            // Act
            var data = InstructionsSysvar.Serialize([first, second], currentInstructionIndex: 1);
            var decodedFirst = InstructionsSysvar.ReadInstruction(data, 0);
            var decodedRelative = InstructionsSysvar.ReadInstructionRelative(data, -1);

            // Assert
            data.Should().Equal(expected);
            InstructionsSysvar.GetInstructionCount(data).Should().Be(2);
            InstructionsSysvar.ReadCurrentInstructionIndex(data).Should().Be(1);
            decodedFirst.ProgramId.Should().Be(first.ProgramId);
            decodedFirst.Accounts.Should().Equal(first.Accounts);
            decodedFirst.Data.Should().Equal(first.Data);
            decodedRelative.ProgramId.Should().Be(first.ProgramId);
        }
    }

    [TestFixture]
    public sealed class WriteCurrentInstructionIndex
    {
        [Test]
        public void CurrentIndex_CanBeUpdatedInPlace()
        {
            // Arrange
            var data = InstructionsSysvar.Serialize([]);

            // Act
            InstructionsSysvar.WriteCurrentInstructionIndex(data, 7);

            // Assert
            InstructionsSysvar.ReadCurrentInstructionIndex(data).Should().Be(7);
        }
    }

    [TestFixture]
    public sealed class GetInstructionCount
    {
        [Test]
        public void TruncatedTable_IsRejectedBeforeAllocation()
        {
            // Arrange
            var truncatedTable = new byte[] { 1, 0, 0 };

            // Act
            var table = () => InstructionsSysvar.GetInstructionCount(truncatedTable);

            // Assert
            table.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class ReadInstruction
    {
        [Test]
        public void ImpossibleAccountCount_IsRejectedBeforeAllocation()
        {
            // Arrange
            var impossibleAccounts = Convert.FromHexString("01000400ffff");

            // Act
            var accounts = () => InstructionsSysvar.ReadInstruction(impossibleAccounts, 0);

            // Assert
            accounts.Should().Throw<FormatException>();
        }

        [Test]
        public void TruncatedInstructionData_IsRejectedBeforeAllocation()
        {
            // Arrange
            var truncatedInstructionData = Convert.FromHexString(
                "010004000000" + string.Concat(Enumerable.Repeat("01", PublicKey.Length)) + "0200aa");

            // Act
            var instructionData = () => InstructionsSysvar.ReadInstruction(truncatedInstructionData, 0);

            // Assert
            instructionData.Should().Throw<FormatException>();
        }

        [Test]
        public void AbsoluteIndex_IsBounded()
        {
            // Arrange
            var data = InstructionsSysvar.Serialize([]);

            // Act
            var absolute = () => InstructionsSysvar.ReadInstruction(data, 0);

            // Assert
            absolute.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class ReadInstructionRelative
    {
        [Test]
        public void RelativeIndex_IsBounded()
        {
            // Arrange
            var data = InstructionsSysvar.Serialize([]);

            // Act
            var relative = () => InstructionsSysvar.ReadInstructionRelative(data, -1);

            // Assert
            relative.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
