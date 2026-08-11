using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Primitives;

public static class HashTests
{
    // solana-sdk/hash/src/lib.rs test vector: 32 bytes whose value is one.
    private const string Sample = "4vJ9JU1bJJE96FWSJKvHsmmFADCg4gpZQff4P3bkLKi";

    [TestFixture]
    public sealed class Constructor
    {
        [Test]
        public void UpstreamKnownVector_RoundTripsBytesAndBase58()
        {
            // Arrange
            var bytes = Enumerable.Repeat((byte)1, Hash.Length).ToArray();

            // Act
            var hash = new Hash(bytes);

            // Assert
            hash.ToBytes().Should().Equal(bytes);
            hash.ToString().Should().Be(Sample);
        }

        [TestCase(0)]
        [TestCase(31)]
        [TestCase(33)]
        public void WrongLength_Throws(int length)
        {
            // Act
            Action act = () => _ = new Hash(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void ValidBase58_RoundTripsToSameString() => Hash.Parse(Sample).ToString().Should().Be(Sample);

        [TestCase("0")]
        [TestCase("abc")]
        public void Invalid_Throws(string input)
        {
            // Act
            Action act = () => Hash.Parse(input);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void OverLongInput_IsNotCopiedIntoTheException()
        {
            // Arrange
            var input = new string('z', 10_000);

            // Act
            Action act = () => Hash.Parse(input);

            // Assert
            var exception = act.Should().Throw<ArgumentException>().Which;
            exception.Message.Length.Should().BeLessThan(256);
            exception.Message.Should().NotContain(input);
            exception.Message.Should().Contain(input.Length.ToString());
        }
    }

    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void ValidBase58_ReturnsTrueAndHash()
        {
            // Act
            var parsed = Hash.TryParse(Sample, out var hash);

            // Assert
            parsed.Should().BeTrue();
            hash.ToString().Should().Be(Sample);
        }

        [TestCase("0")]
        [TestCase("abc")]
        [TestCase(null)]
        [TestCase("")]
        public void Invalid_ReturnsFalseAndDefault(string? input)
        {
            // Act
            var parsed = Hash.TryParse(input, out var hash);

            // Assert
            parsed.Should().BeFalse();
            hash.Should().Be(default(Hash));
        }
    }

    [TestFixture]
    public new sealed class Equals
    {
        [Test]
        public void SameBytes_AreEqual()
        {
            // Arrange
            var a = Hash.Parse(Sample);
            var b = new Hash(a.ToBytes());

            // Act & Assert
            a.Should().Be(b);
        }

        [Test]
        public void DifferentBytes_AreNotEqual()
        {
            // Arrange
            var a = Hash.Parse(Sample);
            var b = default(Hash);

            // Act & Assert
            a.Should().NotBe(b);
        }

        [Test]
        public void Default_EqualsAllZeroHash()
            => default(Hash).Should().Be(new Hash(new byte[Hash.Length]));
    }

    [TestFixture]
    public sealed class EqualityOperators
    {
        [Test]
        public void SameBytes_AreEqual()
        {
            // Arrange
            var a = Hash.Parse(Sample);
            var b = new Hash(a.ToBytes());

            // Act & Assert
            (a == b).Should().BeTrue();
        }

        [Test]
        public void DifferentBytes_AreNotEqual()
        {
            // Arrange
            var a = Hash.Parse(Sample);
            var b = default(Hash);

            // Act & Assert
            (a != b).Should().BeTrue();
        }
    }

    [TestFixture]
    public new sealed class GetHashCode
    {
        [Test]
        public void SameBytes_HaveSameHashCode()
        {
            // Arrange
            var a = Hash.Parse(Sample);
            var b = new Hash(a.ToBytes());

            // Act & Assert
            a.GetHashCode().Should().Be(b.GetHashCode());
        }
    }

    [TestFixture]
    public sealed class CopyTo
    {
        [Test]
        public void CopyTo_WritesAllBytes()
        {
            // Arrange
            var hash = Hash.Parse(Sample);
            var destination = new byte[Hash.Length];

            // Act
            hash.CopyTo(destination);

            // Assert
            destination.Should().Equal(Enumerable.Repeat((byte)1, Hash.Length));
        }

        [Test]
        public void CopyTo_DestinationTooSmall_Throws()
        {
            // Arrange
            var hash = Hash.Parse(Sample);

            // Act
            var act = () => hash.CopyTo(new byte[Hash.Length - 1]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void Serializes_ToBase58String()
            => JsonSerializer.Serialize(Hash.Parse(Sample)).Should().Be($"\"{Sample}\"");
    }

    [TestFixture]
    public sealed class Deserialize
    {
        [Test]
        public void Deserializes_FromBase58String()
            => JsonSerializer.Deserialize<Hash>($"\"{Sample}\"").Should().Be(Hash.Parse(Sample));

        [Test]
        public void Deserialize_Invalid_Throws()
        {
            // Act
            Action act = static () => JsonSerializer.Deserialize<Hash>("\"0\"");

            // Assert
            act.Should().Throw<JsonException>();
        }

        [Test]
        public void Deserialize_OverLongInput_IsRejectedBeforeMaterializingIt()
        {
            // Arrange
            var input = new string('z', 1_000_000);
            var json = JsonSerializer.Serialize(input);
            try
            {
                _ = JsonSerializer.Deserialize<Hash>($"\"{new string('z', Hash.MaxBase58Length + 1)}\"");
            }
            catch (JsonException)
            {
                // Warm serializer metadata and the converter's oversized-token path before measuring.
            }

            // Act
            JsonException? exception = null;
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                _ = JsonSerializer.Deserialize<Hash>(json);
            }
            catch (JsonException error)
            {
                exception = error;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Assert
            exception.Should().NotBeNull();
            exception.Message.Length.Should().BeLessThan(256);
            exception.Message.Should().NotContain(input);
            allocated.Should().BeLessThan(256_000, "the oversized token should not become a UTF-16 string");
        }

        [Test]
        public void Deserialize_MaximumLengthEscapedValue_RemainsAccepted()
        {
            // Arrange: every base58 character may legally be represented as a six-byte JSON \uXXXX escape.
            var bytes = Enumerable.Repeat(byte.MaxValue, Hash.Length).ToArray();
            var escaped = string.Concat(new Hash(bytes).ToString().Select(static character => $"\\u{(int)character:x4}"));

            // Act
            var hash = JsonSerializer.Deserialize<Hash>($"\"{escaped}\"");

            // Assert
            hash.ToBytes().Should().Equal(bytes);
        }

        [TestCase("123")]
        [TestCase("true")]
        [TestCase("{}")]
        [TestCase("[]")]
        public void Deserialize_NonString_ThrowsJsonException(string json)
        {
            // Act
            Action act = () => JsonSerializer.Deserialize<Hash>(json);

            // Assert
            act.Should().Throw<JsonException>();
        }
    }
}
