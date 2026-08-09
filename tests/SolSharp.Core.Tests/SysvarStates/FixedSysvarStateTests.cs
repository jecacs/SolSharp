using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.SysvarStates;

namespace SolSharp.Core.Tests.SysvarStates;

public static class ClockSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesEveryField()
        {
            // Arrange
            var data = Convert.FromHexString(
                "0100000000000000FEFFFFFFFFFFFFFF03000000000000000400000000000000FBFFFFFFFFFFFFFF");

            // Act
            var state = ClockSysvarState.Parse(data);

            // Assert
            state.Should().Be(new ClockSysvarState(1, -2, 3, 4, -5));
            ClockSysvarState.DataLength.Should().Be(40);
        }
    }
}

public static class RentSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void CurrentPinnedDefaultVector_DecodesRetainedWireFields()
        {
            // Arrange - Rent::default() at solana-rent 4.1 keeps threshold 1.0 and burn percent 50.
            var data = Convert.FromHexString("301B000000000000000000000000F03F32");

            // Act
            var state = RentSysvarState.Parse(data);

            // Assert
            state.Should().Be(new RentSysvarState(6_960, 1.0, 50));
            RentSysvarState.DataLength.Should().Be(17);
        }
    }
}

public static class EpochScheduleSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesExactFieldOrder()
        {
            // Arrange
            var data = Convert.FromHexString(
                "010000000000000002000000000000000103000000000000000400000000000000");

            // Act
            var state = EpochScheduleSysvarState.Parse(data);

            // Assert
            state.Should().Be(new EpochScheduleSysvarState(1, 2, true, 3, 4));
            EpochScheduleSysvarState.DataLength.Should().Be(33);
        }

        [Test]
        public void NonCanonicalBool_IsRejected()
        {
            // Arrange
            var data = new byte[EpochScheduleSysvarState.DataLength];
            data[16] = 2;

            // Act & Assert
            FluentActions.Invoking(() => EpochScheduleSysvarState.Parse(data))
                .Should().Throw<ArgumentException>();
        }
    }
}

public static class EpochRewardsSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesHashUInt128AndTail()
        {
            // Arrange
            var data = Convert.FromHexString(
                "01000000000000000200000000000000" +
                "0303030303030303030303030303030303030303030303030303030303030303" +
                "04000000000000000500000000000000" +
                "0600000000000000070000000000000001");

            // Act
            var state = EpochRewardsSysvarState.Parse(data);

            // Assert
            state.DistributionStartingBlockHeight.Should().Be(1);
            state.NumberOfPartitions.Should().Be(2);
            state.ParentBlockhash.ToBytes().Should().OnlyContain(value => value == 3);
            state.TotalPoints.Should().Be(((UInt128)5 << 64) | 4);
            state.TotalRewards.Should().Be(6);
            state.DistributedRewards.Should().Be(7);
            state.Active.Should().BeTrue();
            EpochRewardsSysvarState.DataLength.Should().Be(81);
        }
    }
}

public static class LastRestartSlotSysvarStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void PinnedBincodeVector_DecodesSlot()
        {
            // Arrange
            var data = Convert.FromHexString("0807060504030201");

            // Act
            var state = LastRestartSlotSysvarState.Parse(data);

            // Assert
            state.LastRestartSlot.Should().Be(0x0102030405060708UL);
            LastRestartSlotSysvarState.DataLength.Should().Be(8);
        }

        [TestCase("08070605040302")]
        [TestCase("080706050403020100")]
        public void NonExactLength_IsRejected(string hex) =>
            // Act & Assert
            FluentActions.Invoking(() => LastRestartSlotSysvarState.Parse(Convert.FromHexString(hex)))
                .Should().Throw<ArgumentException>();
    }
}
