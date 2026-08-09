using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class SignatureTests
{
    // solana-sdk/signature/src/lib.rs test_signature_fromstr vector.
    private const string UpstreamBase58 =
        "34UR3rLRtnsQVHNQ49AtUzYP5mLWsvoEBPYMGa1dmSHvg6pZup8ysqtM5LEg2vbcGfi91Upu2JkLyw3uRm7Y1fqX";

    private static readonly byte[] UpstreamBytes = Convert.FromHexString(
        "67075860CB8CBF2FE7251EDC3D235D70E102050B9E69F69385406DFC77496CF8"
        + "A7F0A012DE03013033435E135B6CE37E6419D4875A3C3D4EBA68163AF24A9406");

    private const string RfcPublicKey = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    private const string RfcSignature =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    [TestFixture]
    public sealed class Construct
    {
        [Test]
        public void UpstreamKnownVector_RoundTripsBytesAndBase58()
        {
            // Act
            var signature = new Signature(UpstreamBytes);

            // Assert
            signature.ToBytes().Should().Equal(UpstreamBytes);
            signature.ToString().Should().Be(UpstreamBase58);
        }

        [TestCase(0)]
        [TestCase(63)]
        [TestCase(65)]
        public void WrongLength_Throws(int length)
        {
            // Act
            Action act = () => _ = new Signature(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void UpstreamKnownVector_RoundTripsToSameBytes()
            => Signature.Parse(UpstreamBase58).ToBytes().Should().Equal(UpstreamBytes);

        [TestCase("0")]
        [TestCase("abc")]
        public void Invalid_Throws(string input)
        {
            // Act
            Action act = () => Signature.Parse(input);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void ValidBase58_ReturnsTrueAndSignature()
        {
            // Act
            var parsed = Signature.TryParse(UpstreamBase58, out var signature);

            // Assert
            parsed.Should().BeTrue();
            signature.ToBytes().Should().Equal(UpstreamBytes);
        }

        [TestCase("0")]
        [TestCase("abc")]
        [TestCase(null)]
        [TestCase("")]
        public void Invalid_ReturnsFalseAndDefault(string? input)
        {
            // Act
            var parsed = Signature.TryParse(input, out var signature);

            // Assert
            parsed.Should().BeFalse();
            signature.Should().Be(default(Signature));
        }
    }

    [TestFixture]
    public sealed class Equality
    {
        [Test]
        public void SameBytes_AreEqual()
        {
            // Arrange
            var a = Signature.Parse(UpstreamBase58);
            var b = new Signature(a.ToBytes());

            // Act & Assert
            a.Should().Be(b);
            (a == b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Test]
        public void DifferentBytes_AreNotEqual()
        {
            // Arrange
            var a = Signature.Parse(UpstreamBase58);
            var b = default(Signature);

            // Act & Assert
            a.Should().NotBe(b);
            (a != b).Should().BeTrue();
        }

        [Test]
        public void Default_EqualsAllZeroSignature()
            => default(Signature).Should().Be(new Signature(new byte[Signature.Length]));
    }

    [TestFixture]
    public sealed class Bytes
    {
        [Test]
        public void CopyTo_WritesAllBytes()
        {
            // Arrange
            var signature = Signature.Parse(UpstreamBase58);
            var destination = new byte[Signature.Length];

            // Act
            signature.CopyTo(destination);

            // Assert
            destination.Should().Equal(UpstreamBytes);
        }

        [Test]
        public void CopyTo_DestinationTooSmall_Throws()
        {
            // Arrange
            var signature = Signature.Parse(UpstreamBase58);

            // Act
            var act = () => signature.CopyTo(new byte[Signature.Length - 1]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Verify
    {
        [Test]
        public void Rfc8032Vector_UsesStrictVerification()
        {
            // Arrange
            var signature = new Signature(Hex(RfcSignature));
            var publicKey = new PublicKey(Hex(RfcPublicKey));

            // Act & Assert
            signature.Verify(publicKey, []).Should().BeTrue();
            signature.Verify(publicKey, "tampered"u8).Should().BeFalse();
        }

        [Test]
        public void SmallOrderR_ReturnsFalse()
        {
            // Arrange: C2SP CCTV vector 5, pinned by Agave's strict-verification regression test.
            var publicKey = new PublicKey(Hex("10eb7c3acfb2bed3e0d6ab89bf5a3d6afddd1176ce4812e38d9fd485058fdb1f"));
            var signature = new Signature(Hex(
                "0000000000000000000000000000000000000000000000000000000000000000" +
                "9472a69cd9a701a50d130ed52189e2455b23767db52cacb8716fb896ffeeac09"));

            // Act & Assert
            signature.Verify(publicKey, "ed25519vectors 3"u8).Should().BeFalse();
        }
    }
}
