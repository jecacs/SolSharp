using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class PublicKeyExtensionsTests
{
    // RFC 8032, Section 7.1 - Ed25519 known-answer test vectors.
    private const string Test1PublicKey = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    private const string Test1Signature =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private const string Test2PublicKey = "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c";
    private const string Test2Message = "72";

    private const string Test2Signature =
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da" +
        "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00";

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static PublicKey Key(string hex) => new(Hex(hex));

    [TestFixture]
    public sealed class Verify
    {
        [Test]
        public void Rfc8032Test1_EmptyMessage_ReturnsTrue()
        {
            Key(Test1PublicKey).Verify([], Hex(Test1Signature)).Should().BeTrue();
        }

        [Test]
        public void Rfc8032Test2_ReturnsTrue()
        {
            Key(Test2PublicKey).Verify(Hex(Test2Message), Hex(Test2Signature)).Should().BeTrue();
        }

        [Test]
        public void TamperedMessage_ReturnsFalse()
        {
            Key(Test2PublicKey).Verify(Hex("73"), Hex(Test2Signature)).Should().BeFalse();
        }

        [Test]
        public void TamperedSignature_ReturnsFalse()
        {
            // Arrange
            var signature = Hex(Test2Signature);
            signature[0] ^= 0x01;

            // Act & Assert
            Key(Test2PublicKey).Verify(Hex(Test2Message), signature).Should().BeFalse();
        }

        [Test]
        public void WrongKey_ReturnsFalse()
        {
            Key(Test1PublicKey).Verify(Hex(Test2Message), Hex(Test2Signature)).Should().BeFalse();
        }

        [TestCase(0)]
        [TestCase(63)]
        [TestCase(65)]
        public void WrongLengthSignature_ReturnsFalse(int length)
        {
            Key(Test1PublicKey).Verify([], new byte[length]).Should().BeFalse();
        }

        [Test]
        public void RoundTripsWithKeypairSignature()
        {
            // Arrange
            using var keypair = Keypair.Generate();
            var message = "round-trip"u8;
            var signature = keypair.Sign(message);

            // Act & Assert
            keypair.PublicKey.Verify(message, signature).Should().BeTrue();
            keypair.PublicKey.Verify("tampered"u8, signature).Should().BeFalse();
        }
    }
}
