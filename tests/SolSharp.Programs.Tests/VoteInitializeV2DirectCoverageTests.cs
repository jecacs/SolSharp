using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class VoteInitializeV2DirectCoverageTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class Constructor
    {
        [Test]
        public void RawCredentials_ExposeEveryPinnedInitializationField()
        {
            // Arrange
            var publicKey = Enumerable.Repeat((byte)3, VoteAuthorization.BlsPublicKeyLength).ToArray();
            var proof = Enumerable.Repeat((byte)4, VoteAuthorization.BlsProofOfPossessionLength).ToArray();

            // Act
            var initialize = new VoteInitializeV2(
                Key(1), Key(2), publicKey, proof, Key(5), 0x1234, 0xabcd);

            // Assert
            initialize.Node.Should().Be(Key(1));
            initialize.AuthorizedVoter.Should().Be(Key(2));
            initialize.BlsPublicKey.ToArray().Should().Equal(publicKey);
            initialize.BlsProofOfPossession.ToArray().Should().Equal(proof);
            initialize.AuthorizedWithdrawer.Should().Be(Key(5));
            initialize.InflationRewardsCommissionBps.Should().Be(0x1234);
            initialize.BlockRevenueCommissionBps.Should().Be(0xabcd);
        }
    }
}
