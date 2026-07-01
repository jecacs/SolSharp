using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class Slip10Tests
{
    [TestFixture]
    public sealed class DeriveEd25519
    {
        // SLIP-0010 ed25519 test vector 1 (seed 000102030405060708090a0b0c0d0e0f).
        [TestCase("m", "2b4be7f19ee27bbf30c667b642d5f4aa69fd169872f8fc3059c08ebae2eb19e7")]
        [TestCase("m/0'", "68e0fe46dfb67e368c75379acec591dad19df3cde26e63b93a8e704f1dade7a3")]
        [TestCase("m/0'/1'", "b1d0bad404bf35da785a64ca1ac54b2617211d2777696fbffaf208f746ae84f2")]
        [TestCase("m/0'/1'/2'/2'/1000000000'", "8f94d394a8e8fd6b1bc2f3f49f5c47e385281d5c17e65324b0f62483e37e8793")]
        public void MatchesSlip10Vector1(string path, string expectedHex)
        {
            // Arrange
            var seed = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");

            // Act
            var key = Slip10.DeriveEd25519(seed, path);

            // Assert
            Convert.ToHexString(key).ToLowerInvariant().Should().Be(expectedHex);
        }

        // SLIP-0010 ed25519 test vector 2 (512-bit seed).
        [TestCase("m", "171cb88b1b3c1db25add599712e36245d75bc65a1a5c9e18d76f9f2b1eab4012")]
        [TestCase("m/0'", "1559eb2bbec5790b0c65d8693e4d0875b1747f4970ae8b650486ed7470845635")]
        [TestCase("m/0'/2147483647'", "ea4f5bfe8694d8bb74b7b59404632fd5968b774ed545e810de9c32a4fb4192f4")]
        public void MatchesSlip10Vector2(string path, string expectedHex)
        {
            // Arrange
            var seed = Convert.FromHexString(
                "fffcf9f6f3f0edeae7e4e1dedbd8d5d2cfccc9c6c3c0bdbab7b4b1aeaba8a5a2" +
                "9f9c999693908d8a8784817e7b7875726f6c696663605d5a5754514e4b484542");

            // Act
            var key = Slip10.DeriveEd25519(seed, path);

            // Assert
            Convert.ToHexString(key).ToLowerInvariant().Should().Be(expectedHex);
        }

        [Test]
        public void NonHardenedSegment_Throws()
        {
            // Act & Assert
            Action act = () => _ = Slip10.DeriveEd25519(new byte[16], "m/44'/501'/0'/0");
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void PathNotStartingWithMaster_Throws()
        {
            // Act & Assert
            Action act = () => _ = Slip10.DeriveEd25519(new byte[16], "44'/501'");
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void EmptySeed_Throws()
        {
            // Act & Assert
            Action act = () => _ = Slip10.DeriveEd25519([], "m/0'");
            act.Should().Throw<ArgumentException>();
        }
    }
}
