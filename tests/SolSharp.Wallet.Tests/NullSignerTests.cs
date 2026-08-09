using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class NullSignerTests
{
    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void ReturnsSolanaZeroSignaturePlaceholder()
        {
            // Arrange
            var publicKey = new PublicKey(Enumerable.Repeat((byte)1, PublicKey.Length).ToArray());
            var signer = new NullSigner(publicKey);

            // Act
            var signature = signer.Sign("ignored"u8);

            // Assert
            signer.PublicKey.Should().Be(publicKey);
            signature.Should().HaveCount(Signature.Length).And.OnlyContain(value => value == 0);
        }

        [Test]
        public void EachCallReturnsIndependentArray()
        {
            // Arrange
            var signer = new NullSigner(default);
            var first = signer.Sign([]);

            // Act
            first[0] = 1;

            // Assert
            signer.Sign([]).Should().OnlyContain(value => value == 0);
        }
    }
}
