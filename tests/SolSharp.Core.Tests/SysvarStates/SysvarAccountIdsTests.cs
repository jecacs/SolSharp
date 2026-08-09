using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;

namespace SolSharp.Core.Tests.SysvarStates;

public static class SysvarsTests
{
    [TestFixture]
    public sealed class Constants
    {
        [Test]
        public void CurrentPinnedSdkExports_MatchCanonicalAddresses()
        {
            // Arrange
            string[] expected =
            [
                "Sysvar1111111111111111111111111111111111111",
                "SysvarC1ock11111111111111111111111111111111",
                "SysvarEpochRewards1111111111111111111111111",
                "SysvarEpochSchedu1e111111111111111111111111",
                "SysvarFees111111111111111111111111111111111",
                "Sysvar1nstructions1111111111111111111111111",
                "SysvarLastRestartS1ot1111111111111111111111",
                "SysvarRecentB1ockHashes11111111111111111111",
                "SysvarRent111111111111111111111111111111111",
                "SysvarRewards111111111111111111111111111111",
                "SysvarS1otHashes111111111111111111111111111",
                "SysvarS1otHistory11111111111111111111111111",
                "SysvarStakeHistory1111111111111111111111111"
            ];

            // Act
            string[] actual =
            [
                Sysvars.Owner,
                Sysvars.Clock,
                Sysvars.EpochRewards,
                Sysvars.EpochSchedule,
                Sysvars.Fees,
                Sysvars.Instructions,
                Sysvars.LastRestartSlot,
                Sysvars.RecentBlockhashes,
                Sysvars.Rent,
                Sysvars.Rewards,
                Sysvars.SlotHashes,
                Sysvars.SlotHistory,
                Sysvars.StakeHistory
            ];

            // Assert
            actual.Should().Equal(expected);
        }
    }
}
