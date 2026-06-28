using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Primitives;

public static class PublicKeyTests
{
    private const string Sample = SolanaProgramIds.TokenProgram;

    [TestFixture]
    public sealed class Construct
    {
        [Test]
        public void ValidBytes_RoundTripToSameBytes()
        {
            // Arrange
            var bytes = new byte[PublicKey.Length];
            Random.Shared.NextBytes(bytes);

            // Act & Assert
            new PublicKey(bytes).ToBytes().Should().Equal(bytes);
        }

        [TestCase(0)]
        [TestCase(31)]
        [TestCase(33)]
        public void WrongLength_Throws(int length)
        {
            // Act
            Action act = () => _ = new PublicKey(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void ValidBase58_RoundTripsToSameString() => PublicKey.Parse(Sample).ToString().Should().Be(Sample);

        [Test]
        public void SystemProgram_IsThirtyTwoZeroBytes() => PublicKey.Parse(SolanaProgramIds.SystemProgram).ToBytes().Should().Equal(new byte[32]);

        [TestCase("0")]   // not in the base58 alphabet
        [TestCase("abc")] // valid alphabet, wrong length
        public void Invalid_Throws(string input)
        {
            // Act
            Action act = () => PublicKey.Parse(input);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void ValidBase58_ReturnsTrueAndKey()
        {
            PublicKey.TryParse(Sample, out var key).Should().BeTrue();
            key.ToString().Should().Be(Sample);
        }

        [TestCase("0")]
        [TestCase("abc")]
        [TestCase(null)]
        [TestCase("")]
        public void Invalid_ReturnsFalseAndDefault(string? input)
        {
            PublicKey.TryParse(input, out var key).Should().BeFalse();
            key.Should().Be(default(PublicKey));
        }
    }

    [TestFixture]
    public sealed class Equality
    {
        [Test]
        public void SameBytes_AreEqual()
        {
            // Arrange
            var a = PublicKey.Parse(Sample);
            var b = new PublicKey(a.ToBytes());

            // Assert
            a.Should().Be(b);
            (a == b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Test]
        public void DifferentBytes_AreNotEqual()
        {
            // Arrange
            var a = PublicKey.Parse(SolanaProgramIds.TokenProgram);
            var b = PublicKey.Parse(SolanaProgramIds.SystemProgram);

            // Assert
            a.Should().NotBe(b);
            (a != b).Should().BeTrue();
        }

        [Test]
        public void Default_EqualsAllZeroKey()
            => default(PublicKey).Should().Be(new PublicKey(new byte[32]));
    }

    [TestFixture]
    public sealed class Bytes
    {
        [Test]
        public void CopyTo_WritesAllBytes()
        {
            // Arrange
            var key = PublicKey.Parse(Sample);
            var expected = key.ToBytes();

            // Act
            var destination = new byte[PublicKey.Length];
            key.CopyTo(destination);

            // Assert
            destination.Should().Equal(expected);
        }

        [Test]
        public void CopyTo_DestinationTooSmall_Throws()
        {
            // Arrange
            var key = PublicKey.Parse(Sample);

            // Act
            Action act = () => key.CopyTo(new byte[PublicKey.Length - 1]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Serialization
    {
        [Test]
        public void Serializes_ToBase58String()
            => JsonSerializer.Serialize(PublicKey.Parse(Sample)).Should().Be($"\"{Sample}\"");

        [Test]
        public void Deserializes_FromBase58String()
            => JsonSerializer.Deserialize<PublicKey>($"\"{Sample}\"").Should().Be(PublicKey.Parse(Sample));

        [Test]
        public void Deserialize_Invalid_Throws()
        {
            Action act = () => JsonSerializer.Deserialize<PublicKey>("\"0\"");
            act.Should().Throw<JsonException>();
        }
    }
}
