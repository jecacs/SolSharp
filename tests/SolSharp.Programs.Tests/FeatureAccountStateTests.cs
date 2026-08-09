using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Programs.Tests;

public static class FeatureAccountStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void RequestedButNotActive_MatchesPinnedSdkDefaultAccount()
        {
            // Arrange
            byte[] data = [0, 0, 0, 0, 0, 0, 0, 0, 0];

            // Act
            var state = FeatureAccountState.Parse(data);

            // Assert
            state.ActivatedAt.Should().BeNull();
            state.IsActive.Should().BeFalse();
        }

        [Test]
        public void Activated_ReadsLittleEndianSlot()
        {
            // Arrange - bincode Option::Some(0x0807060504030201).
            byte[] data = [1, 1, 2, 3, 4, 5, 6, 7, 8];

            // Act
            var state = FeatureAccountState.Parse(data);

            // Assert
            state.ActivatedAt.Should().Be(0x0807060504030201UL);
            state.IsActive.Should().BeTrue();
        }

        [TestCase(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 })]
        [TestCase(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0 })]
        public void MalformedData_IsRejected(byte[] data)
        {
            // Act
            var parsed = FeatureAccountState.TryParse(data, out var state);

            // Assert
            parsed.Should().BeFalse();
            state.Should().BeNull();
        }
    }
}
