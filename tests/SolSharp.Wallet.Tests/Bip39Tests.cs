using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class Bip39Tests
{
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    [TestFixture]
    public sealed class ToSeed
    {
        // BIP-39 reference vectors (Trezor test set, English wordlist).
        [Test]
        public void EmptyPassphrase_MatchesReferenceVector()
        {
            // Act
            var seed = Bip39.ToSeed(Mnemonic);

            // Assert
            Convert.ToHexString(seed).ToLowerInvariant().Should().Be(
                "5eb00bbddcf069084889a8ab9155568165f5c453ccb85e70811aaed6f6da5fc1" +
                "9a5ac40b389cd370d086206dec8aa6c43daea6690f20ad3d8d48b2d2ce9e38e4");
        }

        [Test]
        public void TrezorPassphrase_MatchesReferenceVector()
        {
            // Act
            var seed = Bip39.ToSeed(Mnemonic, "TREZOR");

            // Assert
            Convert.ToHexString(seed).ToLowerInvariant().Should().Be(
                "c55257c360c07c72029aebc1b53c05ed0362ada38ead3e3e9efa3708e5349553" +
                "1f09a6987599d18264c1e1c92f2cf141630c7a3c4ab7c81b2f001698e7463b04");
        }

        [Test]
        public void WhitespaceMnemonic_Throws()
        {
            // Act & Assert
            Action act = () => _ = Bip39.ToSeed("   ");
            act.Should().Throw<ArgumentException>();
        }
    }
}
