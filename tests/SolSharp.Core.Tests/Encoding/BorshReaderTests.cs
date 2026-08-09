using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Encoding;

public static class BorshReaderTests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    [TestFixture]
    public sealed class Read
    {
        [Test]
        public void ReadsBorshPrimitivesInOrder()
        {
            // Arrange
            var data = Convert.FromHexString(
                "2a" + // u8 = 42
                "78563412" + // u32 = 0x12345678
                "40420f0000000000" + // u64 = 1_000_000
                "01" + // bool = true
                "01" + "0700000000000000" + // Option Some, u64 = 7
                "00" + // Option None
                "02000000" + "6869" + // string "hi" (length 2, then "hi")
                "0909090909090909090909090909090909090909090909090909090909090909" + // pubkey [9]*32
                "03000000" + "010203");     // Vec<u8> length 3, then 1, 2, 3

            // Act
            var reader = new BorshReader(data);

            // Assert
            reader.ReadU8().Should().Be(42);
            reader.ReadU32().Should().Be(0x12345678u);
            reader.ReadU64().Should().Be(1_000_000ul);
            reader.ReadBool().Should().BeTrue();

            reader.ReadOption().Should().BeTrue();
            reader.ReadU64().Should().Be(7ul);
            reader.ReadOption().Should().BeFalse();

            reader.ReadString().Should().Be("hi");
            reader.ReadPublicKey().Should().Be(Pk(9));

            reader.ReadLength().Should().Be(3);
            reader.ReadU8().Should().Be(1);
            reader.ReadU8().Should().Be(2);
            reader.ReadU8().Should().Be(3);

            reader.Remaining.Should().Be(0);
        }
    }

    [TestFixture]
    public sealed class Bounds
    {
        [Test]
        public void ReadingPastEnd_Throws()
        {
            // Act
            var act = () => ReadU64From([1, 2]);

            // Assert
            act.Should().Throw<FormatException>();
        }

        private static void ReadU64From(byte[] data) => new BorshReader(data).ReadU64();
    }

    [TestFixture]
    public sealed class ReadBool
    {
        [Test]
        public void Zero_ReturnsFalse()
        {
            // Arrange
            var reader = new BorshReader([0]);

            // Act
            var value = reader.ReadBool();

            // Assert
            value.Should().BeFalse();
        }

        [TestCase((byte)0x02)]
        [TestCase((byte)0xff)]
        public void NonCanonicalDiscriminant_ThrowsFormatException(byte value)
        {
            // Arrange
            var data = new[] { value };

            // Act
            var act = () => ReadBoolFrom(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*must be 0 or 1*");
        }

        private static void ReadBoolFrom(byte[] data) => new BorshReader(data).ReadBool();
    }

    [TestFixture]
    public sealed class ReadOption
    {
        [TestCase((byte)0x02)]
        [TestCase((byte)0xff)]
        public void NonCanonicalDiscriminant_ThrowsFormatException(byte value)
        {
            // Arrange
            var data = new[] { value };

            // Act
            var act = () => ReadOptionFrom(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*must be 0 or 1*");
        }

        private static void ReadOptionFrom(byte[] data) => new BorshReader(data).ReadOption();
    }

    [TestFixture]
    public sealed class ReadString
    {
        [Test]
        public void MultibyteScalars_DecodesUtf8Vector()
        {
            // Arrange
            var data = Convert.FromHexString("0a00000041c2a2e282acf0908d88");
            var reader = new BorshReader(data);

            // Act
            var value = reader.ReadString();

            // Assert
            value.Should().Be("A¢€𐍈");
            reader.Remaining.Should().Be(0);
        }

        [Test]
        public void InvalidUtf8_ThrowsFormatException()
        {
            // Arrange
            var data = Convert.FromHexString("01000000ff");

            // Act
            var act = () => ReadStringFrom(data);

            // Assert
            act.Should().Throw<FormatException>()
                .WithMessage("*invalid UTF-8*")
                .WithInnerException<System.Text.DecoderFallbackException>();
        }

        private static void ReadStringFrom(byte[] data) => new BorshReader(data).ReadString();
    }
}
