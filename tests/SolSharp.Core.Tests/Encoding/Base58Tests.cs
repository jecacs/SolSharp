using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Encoding;

public static class Base58Tests
{
    [TestFixture]
    public sealed class Encode
    {
        [Test]
        public void ThirtyTwoZeroBytes_ReturnsSystemProgramId()
            => Base58.Encode(new byte[32]).Should().Be(SolanaProgramIds.SystemProgram);

        [Test]
        public void Empty_ReturnsEmptyString()
            => Base58.Encode([]).Should().BeEmpty();

        [TestCase(new byte[] { 0x00 }, "1")]
        [TestCase(new byte[] { 0x00, 0x00 }, "11")]
        [TestCase(new byte[] { 0x00, 0x01 }, "12")]
        public void LeadingZeroBytes_BecomeLeadingOnes(byte[] input, string expected)
            => Base58.Encode(input).Should().Be(expected);
    }

    [TestFixture]
    public sealed class Decode
    {
        [Test]
        public void SystemProgramId_ReturnsThirtyTwoZeroBytes()
            => Base58.Decode(SolanaProgramIds.SystemProgram).Should().Equal(new byte[32]);

        [Test]
        public void RealPubkey_RoundTripsBackToSameString()
        {
            // Act
            var decoded = Base58.Decode(SolanaProgramIds.TokenProgram);

            // Assert
            decoded.Should().HaveCount(32);
            Base58.Encode(decoded).Should().Be(SolanaProgramIds.TokenProgram);
        }

        [Test]
        public void RandomBytes_RoundTrip()
        {
            // Arrange
            var rng = new Random(1234);

            // Act & Assert
            for (var i = 0; i < 500; i++)
            {
                var bytes = new byte[rng.Next(0, 64)];
                rng.NextBytes(bytes);

                Base58.Decode(Base58.Encode(bytes)).Should().Equal(bytes);
            }
        }

        [Test]
        public void InvalidLargeInput_IsNotCopiedIntoTheException()
        {
            // Arrange
            var input = new string('0', 10_000);

            // Act
            Action act = () => Base58.Decode(input);

            // Assert
            var exception = act.Should().Throw<FormatException>().Which;
            exception.Message.Length.Should().BeLessThan(256);
            exception.Message.Should().NotContain(input);
            exception.Message.Should().Contain(input.Length.ToString());
        }
    }

    [TestFixture]
    public sealed class TryDecode
    {
        [Test]
        public void ValidString_ReturnsTrueAndBytes()
        {
            Base58.TryDecode(SolanaProgramIds.TokenProgram, out var bytes).Should().BeTrue();
            bytes.Should().HaveCount(32);
        }

        [TestCase("0")] // not in the base58 alphabet
        [TestCase("O")]
        [TestCase("I")]
        [TestCase("l")]
        [TestCase("bad string!")]
        public void NonAlphabet_ReturnsFalseAndEmpty(string input)
        {
            Base58.TryDecode(input, out var bytes).Should().BeFalse();
            bytes.Should().BeEmpty();
        }

        [TestCase(null)]
        [TestCase("")]
        public void NullOrEmpty_ReturnsFalse(string? input)
        {
            Base58.TryDecode(input, out var bytes).Should().BeFalse();
            bytes.Should().BeEmpty();
        }
    }

    [TestFixture]
    public sealed class BoundedTryDecode
    {
        private const int PublicKeyMaxBase58Length = 44;

        [Test]
        public void OverLongInput_IsRejectedWithoutDecoding()
        {
            // Arrange: base58 decoding is quadratic, so an over-long string must be rejected on length
            // alone. A 200k-character input took ~24 s to reject before the bound existed.
            var input = new string('z', 200_000);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var decoded = Base58.TryDecode(input, PublicKeyMaxBase58Length, out var bytes);
            stopwatch.Stop();

            // Assert
            decoded.Should().BeFalse();
            bytes.Should().BeEmpty();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(1_000);
        }

        [Test]
        public void InputWithinTheBound_StillDecodes()
        {
            // Act
            var decoded = Base58.TryDecode("TokenzQdBNbLqP5VEhdkAS6EPFLC1PHnBqCXEpPxuEb", PublicKeyMaxBase58Length, out var bytes);

            // Assert
            decoded.Should().BeTrue();
            bytes.Should().HaveCount(32);
        }

        [Test]
        public void NegativeBound_Throws() =>
            // Act & Assert
            FluentActions.Invoking(static () => Base58.TryDecode("abc", -1, out _))
                .Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestFixture]
    public sealed class BoundedDecode
    {
        [Test]
        public void OverLongInput_ThrowsFormatException() =>
            // Act & Assert
            FluentActions.Invoking(static () => Base58.Decode(new('z', 100), 44))
                .Should().Throw<FormatException>().WithMessage("*at most 44*");
    }

    [TestFixture]
    public sealed class FixedWidthCodec
    {
        [Test]
        public void MaximumThirtyTwoByteValue_MatchesKnownBitcoinVector()
        {
            // Arrange
            var bytes = Enumerable.Repeat(byte.MaxValue, PublicKey.Length).ToArray();

            // Act
            var encoded = Base58.Encode(bytes);
            var parsed = PublicKey.TryParse(encoded, out var key);

            // Assert
            encoded.Should().Be("JEKNVnkbo3jma5nREBBJCDoXFVeKkD56V3xKrvRmWxFG");
            parsed.Should().BeTrue();
            key.ToBytes().Should().Equal(bytes);
        }

        [Test]
        public void TwoToThePowerOf256_IsRejectedEvenThoughItsTextFitsTheCharacterBound()
        {
            // Arrange: 2^256 needs 33 bytes but still encodes to 44 base58 characters, so the limb
            // overflow check rather than the cheap text-length check must reject it.
            var bytes = new byte[PublicKey.Length + 1];
            bytes[0] = 1;
            var encoded = Base58.Encode(bytes);

            // Act
            var parsed = PublicKey.TryParse(encoded, out var key);

            // Assert
            encoded.Should().HaveLength(PublicKey.MaxBase58Length);
            parsed.Should().BeFalse();
            key.Should().Be(default(PublicKey));
        }

        [TestCase(31, false)]
        [TestCase(32, true)]
        [TestCase(33, false)]
        public void LeadingOnes_MustDecodeToExactlyThirtyTwoZeroBytes(int count, bool expected)
            => PublicKey.TryParse(new('1', count), out _).Should().Be(expected);

        [Test]
        public void LeadingOnesPlusAValue_PreserveTheExactDecodedWidth()
        {
            // Arrange
            var valid = new string('1', PublicKey.Length - 1) + "2";
            var tooWide = new string('1', PublicKey.Length) + "2";
            var expected = new byte[PublicKey.Length];
            expected[^1] = 1;

            // Act & Assert
            PublicKey.TryParse(valid, out var key).Should().BeTrue();
            key.ToBytes().Should().Equal(expected);
            PublicKey.TryParse(tooWide, out _).Should().BeFalse();
        }
    }
}
