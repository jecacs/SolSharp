using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.SysvarStates;

namespace SolSharp.Core.Tests.SysvarStates;

public static class SlotHashesSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesEntriesInWireOrder()
        {
            // Arrange
            var data = Convert.FromHexString(
                "02000000000000000900000000000000" +
                "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20" +
                "0800000000000000" +
                "201F1E1D1C1B1A191817161514131211100F0E0D0C0B0A090807060504030201");

            // Act
            var state = SlotHashesSysvarState.Parse(data);

            // Assert
            state.Entries.Select(static entry => entry.Slot).Should().Equal(9, 8);
            state.Entries[0].Hash.ToBytes().Should().Equal(Enumerable.Range(1, 32).Select(static value => (byte)value));
            SlotHashesSysvarState.MaximumEntries.Should().Be(512);
            SlotHashesSysvarState.MaximumDataLength.Should().Be(20_488);
        }

        [Test]
        public void CountAboveRuntimeMaximum_IsRejectedBeforeAllocation()
        {
            // Arrange
            var data = Convert.FromHexString("0102000000000000");

            // Act & Assert
            FluentActions.Invoking(() => SlotHashesSysvarState.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage("*exceeds the maximum*");
        }

        [Test]
        public void CanonicalZeroPadding_IsAccepted()
        {
            // Arrange: the runtime allocates the account at its canonical size and serializes into it, so a
            // ring that is not yet full is followed by zero bytes. Rust's bincode decode ignores that tail.
            var data = new byte[SlotHashesSysvarState.MaximumDataLength];
            Convert.FromHexString(
                    "01000000000000000900000000000000" +
                    "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20")
                .CopyTo(data, 0);

            // Act
            var state = SlotHashesSysvarState.Parse(data);

            // Assert
            state.Entries.Should().HaveCount(1);
            state.Entries[0].Slot.Should().Be(9);
        }

        [Test]
        public void NonZeroTrailingBytes_AreStillRejected()
        {
            // Arrange
            var data = new byte[SlotHashesSysvarState.MaximumDataLength];
            Convert.FromHexString(
                    "01000000000000000900000000000000" +
                    "0102030405060708090A0B0C0D0E0F101112131415161718191A1B1C1D1E1F20")
                .CopyTo(data, 0);
            data[^1] = 0x42;

            // Act & Assert
            FluentActions.Invoking(() => SlotHashesSysvarState.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage("*not zero padding*");
        }

        [Test]
        public void DataLongerThanTheCanonicalAccount_IsRejected() =>
            // Act & Assert
            FluentActions.Invoking(static () => SlotHashesSysvarState.Parse(new byte[SlotHashesSysvarState.MaximumDataLength + 1]))
                .Should().Throw<ArgumentException>().WithMessage("*exceeds*");
    }
}

public static class StakeHistorySysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesEpochAndTotals()
        {
            // Arrange
            var data = Convert.FromHexString(
                "0200000000000000" +
                "09000000000000000A000000000000000B000000000000000C00000000000000" +
                "08000000000000000D000000000000000E000000000000000F00000000000000");

            // Act
            var state = StakeHistorySysvarState.Parse(data);

            // Assert
            state.Entries.Should().Equal(
                new StakeHistoryEpoch(9, new(10, 11, 12)),
                new StakeHistoryEpoch(8, new(13, 14, 15)));
            StakeHistorySysvarState.MaximumEntries.Should().Be(512);
            StakeHistorySysvarState.MaximumDataLength.Should().Be(16_392);
        }

        [Test]
        public void TruncatedEntry_IsRejected()
        {
            // Arrange
            var data = Convert.FromHexString("01000000000000000100000000000000");

            // Act & Assert
            FluentActions.Invoking(() => StakeHistorySysvarState.Parse(data))
                .Should().Throw<ArgumentException>();
        }

        [Test]
        public void CanonicalZeroPadding_IsAccepted()
        {
            // Arrange: a cluster younger than 512 epochs holds fewer entries, zero-padded to the canonical size.
            var data = new byte[StakeHistorySysvarState.MaximumDataLength];
            Convert.FromHexString(
                    "0100000000000000" +
                    "09000000000000000A000000000000000B000000000000000C00000000000000")
                .CopyTo(data, 0);

            // Act
            var state = StakeHistorySysvarState.Parse(data);

            // Assert
            state.Entries.Should().Equal(new StakeHistoryEpoch(9, new(10, 11, 12)));
        }

        [Test]
        public void NonZeroTrailingBytes_AreStillRejected()
        {
            // Arrange
            var data = new byte[StakeHistorySysvarState.MaximumDataLength];
            Convert.FromHexString(
                    "0100000000000000" +
                    "09000000000000000A000000000000000B000000000000000C00000000000000")
                .CopyTo(data, 0);
            data[^1] = 0x42;

            // Act & Assert
            FluentActions.Invoking(() => StakeHistorySysvarState.Parse(data))
                .Should().Throw<ArgumentException>().WithMessage("*not zero padding*");
        }
    }
}
