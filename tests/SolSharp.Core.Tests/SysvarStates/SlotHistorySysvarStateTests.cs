using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.SysvarStates;

namespace SolSharp.Core.Tests.SysvarStates;

public static class SlotHistorySysvarStateTests
{
    private static byte[] BuildVector(ulong firstBlock, ulong nextSlot)
    {
        var data = new byte[SlotHistorySysvarState.DataLength];
        data[0] = 1;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(1), SlotHistorySysvarState.BlockCount);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(9), firstBlock);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(131_081), SlotHistorySysvarState.MaximumEntries);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(131_089), nextSlot);
        return data;
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedDefaultShapeVector_DecodesWincodeBitVectorAndNextSlot()
        {
            // Arrange
            var data = BuildVector(firstBlock: 5, nextSlot: 21);

            // Act
            var state = SlotHistorySysvarState.Parse(data);

            // Assert
            state.NextSlot.Should().Be(21);
            state.OldestSlot.Should().Be(0);
            state.NewestSlot.Should().Be(20);
            SlotHistorySysvarState.MaximumEntries.Should().Be(1_048_576);
            SlotHistorySysvarState.BlockCount.Should().Be(16_384);
            SlotHistorySysvarState.DataLength.Should().Be(131_097);
        }

        [Test]
        public void NonCanonicalBitVectorMetadata_IsRejected()
        {
            // Arrange
            var optionNone = BuildVector(firstBlock: 1, nextSlot: 1);
            optionNone[0] = 0;
            var wrongBlockCount = BuildVector(firstBlock: 1, nextSlot: 1);
            BinaryPrimitives.WriteUInt64LittleEndian(wrongBlockCount.AsSpan(1), 16_383);
            var wrongBitLength = BuildVector(firstBlock: 1, nextSlot: 1);
            BinaryPrimitives.WriteUInt64LittleEndian(wrongBitLength.AsSpan(131_081), 1_048_575);

            // Act & Assert
            FluentActions.Invoking(() => SlotHistorySysvarState.Parse(optionNone))
                .Should().Throw<ArgumentException>();
            FluentActions.Invoking(() => SlotHistorySysvarState.Parse(wrongBlockCount))
                .Should().Throw<ArgumentException>();
            FluentActions.Invoking(() => SlotHistorySysvarState.Parse(wrongBitLength))
                .Should().Throw<ArgumentException>();
        }

        [TestCase(131_096)]
        [TestCase(131_098)]
        public void NonExactAccountLength_IsRejectedBeforeBlockAllocation(int length)
        {
            // Arrange
            var data = new byte[length];

            // Act & Assert
            FluentActions.Invoking(() => SlotHistorySysvarState.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage("*exactly 131097 bytes*");
        }
    }

    [TestFixture]
    public sealed class Check
    {
        [Test]
        public void PinnedRuntimeOrdering_DistinguishesFoundMissingFutureAndTooOld()
        {
            // Arrange - bits 0 and 2 are set, and the retained range wraps so slots below 2 are too old.
            var state = SlotHistorySysvarState.Parse(
                BuildVector(firstBlock: 5, nextSlot: SlotHistorySysvarState.MaximumEntries + 2));

            // Act
            var tooOld = state.Check(1);
            var found = state.Check(2);
            var missing = state.Check(3);
            var future = state.Check(SlotHistorySysvarState.MaximumEntries + 2);

            // Assert
            tooOld.Should().Be(SlotHistoryCheck.TooOld);
            found.Should().Be(SlotHistoryCheck.Found);
            missing.Should().Be(SlotHistoryCheck.NotFound);
            future.Should().Be(SlotHistoryCheck.Future);
        }
    }
}
