using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class KeypairParsingTests
{
    // RFC 8032 Test 1, encoded in the formats wallets and solana-keygen use.
    private const string PublicKeyHex = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    private const string SignatureHex =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private const string SecretBase58 =
        "49W385L4rePHy6PAaQUovbD2aacgN4HsKXSMeUzRg4fmwXszN91JuMFrQRj3vMDpZuRF3ZknQBuRBoWQJEfXstMw";

    private const string SeedBase58 = "BbMQkQYZspmkytduTWvXEtc4mMURjsekJDvty2WtKeSb";

    private const string SecretJson =
        "[157,97,177,157,239,253,90,96,186,132,74,244,146,236,44,196,68,73,197,105,123,50,105,25,112,59,172,3,28,174,127,96," +
        "215,90,152,1,130,177,10,183,213,75,254,211,201,100,7,58,14,225,114,243,218,166,35,37,175,2,26,104,247,7,81,26]";

    private static PublicKey ExpectedPublicKey => new(Convert.FromHexString(PublicKeyHex));

    [TestFixture]
    public sealed class FromBase58String
    {
        [Test]
        public void SecretKey_DerivesPublicKeyAndSigns()
        {
            using var keypair = Keypair.FromBase58String(SecretBase58);

            keypair.PublicKey.Should().Be(ExpectedPublicKey);
            keypair.Sign([]).Should().Equal(Convert.FromHexString(SignatureHex));
        }

        [Test]
        public void Seed_DerivesPublicKey()
        {
            using var keypair = Keypair.FromBase58String(SeedBase58);
            keypair.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [Test]
        public void SurroundingWhitespace_IsTolerated()
        {
            using var keypair = Keypair.FromBase58String($"  {SecretBase58}\n");
            keypair.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [Test]
        public void NotBase58_Throws()
        {
            Action act = () => _ = Keypair.FromBase58String("O0Il");
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void WrongDecodedLength_Throws()
        {
            // "1111111111" is base58 for ten zero bytes - a valid string of the wrong length.
            Action act = () => _ = Keypair.FromBase58String("1111111111");
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class FromJsonArray
    {
        [Test]
        public void SecretKeyArray_DerivesPublicKeyAndSigns()
        {
            using var keypair = Keypair.FromJsonArray(SecretJson);

            keypair.PublicKey.Should().Be(ExpectedPublicKey);
            keypair.Sign([]).Should().Equal(Convert.FromHexString(SignatureHex));
        }

        [TestCase("[300]")]
        [TestCase("[-1]")]
        public void ValueOutOfByteRange_Throws(string json)
        {
            Action act = () => _ = Keypair.FromJsonArray(json);
            act.Should().Throw<FormatException>();
        }

        [TestCase("{}")]
        [TestCase("[1,2")]
        public void NotANumberArray_Throws(string json)
        {
            Action act = () => _ = Keypair.FromJsonArray(json);
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void WrongLength_Throws()
        {
            Action act = () => _ = Keypair.FromJsonArray("[1,2,3]");
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void Base58_IsDetected()
        {
            using var keypair = Keypair.Parse(SecretBase58);
            keypair.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [Test]
        public void JsonArray_IsDetected()
        {
            using var keypair = Keypair.Parse(SecretJson);
            keypair.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [Test]
        public void LeadingWhitespaceBeforeJson_IsDetected()
        {
            using var keypair = Keypair.Parse($"  {SecretJson}");
            keypair.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void NullEmptyOrWhitespace_Throws(string text)
        {
            Action act = () => _ = Keypair.Parse(text);
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void ValidKey_ReturnsTrueAndKeypair()
        {
            Keypair.TryParse(SecretBase58, out var keypair).Should().BeTrue();

            using (keypair)
                keypair!.PublicKey.Should().Be(ExpectedPublicKey);
        }

        [TestCase("O0Il")]
        [TestCase(null)]
        public void InvalidKey_ReturnsFalseAndNull(string? text)
        {
            Keypair.TryParse(text, out var keypair).Should().BeFalse();
            keypair.Should().BeNull();
        }
    }
}
