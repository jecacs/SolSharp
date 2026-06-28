using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Encoding;

namespace SolSharp.Core.Tests.Encoding;

public static class ShortVecTests
{
    // Canonical compact-u16 encodings, shared by Encode and Decode fixtures.
    public static IEnumerable<TestCaseData> Vectors()
    {
        yield return new TestCaseData(0, new byte[] { 0x00 });
        yield return new TestCaseData(1, new byte[] { 0x01 });
        yield return new TestCaseData(127, new byte[] { 0x7f });
        yield return new TestCaseData(128, new byte[] { 0x80, 0x01 });
        yield return new TestCaseData(255, new byte[] { 0xff, 0x01 });
        yield return new TestCaseData(16383, new byte[] { 0xff, 0x7f });
        yield return new TestCaseData(16384, new byte[] { 0x80, 0x80, 0x01 });
        yield return new TestCaseData(65535, new byte[] { 0xff, 0xff, 0x03 });
    }

    public static IEnumerable<TestCaseData> MalformedInputs()
    {
        yield return new TestCaseData(new byte[] { }).SetName("Empty");
        yield return new TestCaseData(new byte[] { 0x80 }).SetName("Truncated");
        yield return new TestCaseData(new byte[] { 0x80, 0x00 }).SetName("NonMinimal");
        yield return new TestCaseData(new byte[] { 0x80, 0x80, 0x80 }).SetName("FourthByteContinuation");
        yield return new TestCaseData(new byte[] { 0xff, 0xff, 0x7f }).SetName("ExceedsU16");
    }

    [TestFixture]
    public sealed class GetByteCount
    {
        [TestCase(0, 1)]
        [TestCase(127, 1)]
        [TestCase(128, 2)]
        [TestCase(16383, 2)]
        [TestCase(16384, 3)]
        [TestCase(65535, 3)]
        public void AtBoundaries_ReturnsExpected(int value, int expected)
        {
            ShortVec.GetByteCount(value).Should().Be(expected);
        }
    }

    [TestFixture]
    public sealed class Encode
    {
        [TestCaseSource(typeof(ShortVecTests), nameof(Vectors))]
        public void ReferenceVector_ProducesExpectedBytes(int value, byte[] expected)
        {
            ShortVec.Encode(value).Should().Equal(expected);
        }

        [TestCase(-1)]
        [TestCase(65536)]
        public void OutOfRange_Throws(int value)
        {
            // Act
            Action act = () => ShortVec.Encode(value);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class Decode
    {
        [TestCaseSource(typeof(ShortVecTests), nameof(Vectors))]
        public void ReferenceVector_ReturnsValueAndLength(int value, byte[] encoded)
        {
            // Act
            var result = ShortVec.Decode(encoded, out var bytesRead);

            // Assert
            result.Should().Be(value);
            bytesRead.Should().Be(encoded.Length);
        }

        [Test]
        public void TrailingBytes_AreIgnored()
        {
            // Act
            var value = ShortVec.Decode([0x80, 0x01, 0xAA, 0xBB], out var bytesRead);

            // Assert
            value.Should().Be(128);
            bytesRead.Should().Be(2);
        }

        [TestCaseSource(typeof(ShortVecTests), nameof(MalformedInputs))]
        public void MalformedInput_ThrowsFormatException(byte[] input)
        {
            // Act
            Action act = () => ShortVec.Decode(input, out _);

            // Assert
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class RoundTrip
    {
        [Test]
        public void EveryValidValue_EncodeThenDecode()
        {
            for (var value = 0; value <= ShortVec.MaxValue; value++)
            {
                var encoded = ShortVec.Encode(value);
                encoded.Length.Should().Be(ShortVec.GetByteCount(value));

                ShortVec.Decode(encoded, out var bytesRead).Should().Be(value);
                bytesRead.Should().Be(encoded.Length);
            }
        }
    }
}
