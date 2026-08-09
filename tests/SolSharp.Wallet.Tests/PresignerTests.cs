using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class PresignerTests
{
    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void UpstreamContract_VerifiesAndReturnsExternalSignature()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(new byte[Keypair.SeedLength]);
            ReadOnlySpan<byte> message = [1];
            var signature = keypair.SignSignature(message);
            var presigner = new Presigner(keypair.PublicKey, signature);

            // Act
            var result = presigner.Sign(message);

            // Assert
            presigner.PublicKey.Should().Be(keypair.PublicKey);
            presigner.Signature.Should().Be(signature);
            result.Should().Equal(signature.ToBytes());
        }

        [Test]
        public void DifferentMessage_ThrowsCryptographicException()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(new byte[Keypair.SeedLength]);
            var presigner = new Presigner(keypair.PublicKey, keypair.SignSignature([1]));

            // Act
            Action act = () => presigner.Sign([2]);

            // Assert
            act.Should().Throw<CryptographicException>();
        }

        [Test]
        public void ReturnedArrayCannotMutateStoredSignature()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(new byte[Keypair.SeedLength]);
            ReadOnlySpan<byte> message = [1];
            var expected = keypair.SignSignature(message);
            var presigner = new Presigner(keypair.PublicKey, expected);
            var returned = presigner.Sign(message);

            // Act
            returned[0] ^= byte.MaxValue;

            // Assert
            presigner.Signature.Should().Be(expected);
            presigner.Sign(message).Should().Equal(expected.ToBytes());
        }
    }
}
