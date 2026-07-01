using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class KeypairMnemonicTests
{
    private const string Mnemonic =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    [TestFixture]
    public sealed class FromMnemonic
    {
        // Reference addresses from solders Keypair.from_seed_phrase_and_passphrase (the solana-keygen scheme).
        [Test]
        public void MatchesSolanaKeygen()
        {
            // Act
            using var keypair = Keypair.FromMnemonic(Mnemonic);

            // Assert
            keypair.PublicKey.ToString().Should().Be("EHqmfkN89RJ7Y33CXM6uCzhVeuywHoJXZZLszBHHZy7o");
        }

        [Test]
        public void WithPassphrase_MatchesSolanaKeygen()
        {
            // Act
            using var keypair = Keypair.FromMnemonic(Mnemonic, "pass");

            // Assert
            keypair.PublicKey.ToString().Should().Be("JAKUTEyfi3ZKNLJ21CRQeE4vPLeHNwjnGqY6gkpk1TNT");
        }
    }

    [TestFixture]
    public sealed class FromMnemonicAtPath
    {
        // Reference addresses derived with bip-utils (Bip32Slip10Ed25519) + solders at the Phantom default paths.
        [Test]
        public void FirstAccount_MatchesPhantomDerivation()
        {
            // Act
            using var keypair = Keypair.FromMnemonicAtPath(Mnemonic, "m/44'/501'/0'/0'");

            // Assert
            keypair.PublicKey.ToString().Should().Be("HAgk14JpMQLgt6rVgv7cBQFJWFto5Dqxi472uT3DKpqk");
        }

        [Test]
        public void SecondAccount_MatchesPhantomDerivation()
        {
            // Act
            using var keypair = Keypair.FromMnemonicAtPath(Mnemonic, "m/44'/501'/1'/0'");

            // Assert
            keypair.PublicKey.ToString().Should().Be("Hh8QwFUA6MtVu1qAoq12ucvFHNwCcVTV7hpWjeY1Hztb");
        }

        [Test]
        public void InvalidPath_Throws()
        {
            // Act & Assert
            Action act = () => _ = Keypair.FromMnemonicAtPath(Mnemonic, "m/44/501");
            act.Should().Throw<FormatException>();
        }
    }
}
