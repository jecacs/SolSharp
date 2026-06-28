using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Tests.Encoding;

public static class BorshWriterTests
{
    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void RoundTripsThroughBorshReader()
        {
            // Arrange
            var pubkey = new PublicKey(Enumerable.Repeat((byte)9, PublicKey.Length).ToArray());

            // Act
            var writer = new BorshWriter();
            writer.WriteU8(42);
            writer.WriteI8(-5);
            writer.WriteU16(0x1234);
            writer.WriteI16(-300);
            writer.WriteU32(0x12345678);
            writer.WriteI32(-70000);
            writer.WriteU64(1_000_000);
            writer.WriteI64(-7);
            writer.WriteU128(UInt128.MaxValue);
            writer.WriteI128((Int128)(-12345));
            writer.WriteBool(true);
            writer.WriteOption(true);
            writer.WriteU64(7);
            writer.WriteOption(false);
            writer.WriteString("hi");
            writer.WritePublicKey(pubkey);
            writer.WriteBytes([0xAA, 0xBB]);   // raw, no length prefix
            writer.WriteByteVector([1, 2, 3]);

            // Assert
            writer.Length.Should().Be(writer.ToArray().Length);

            var reader = new BorshReader(writer.ToArray());
            reader.ReadU8().Should().Be(42);
            reader.ReadI8().Should().Be(-5);
            reader.ReadU16().Should().Be(0x1234);
            reader.ReadI16().Should().Be(-300);
            reader.ReadU32().Should().Be(0x12345678);
            reader.ReadI32().Should().Be(-70000);
            reader.ReadU64().Should().Be(1_000_000);
            reader.ReadI64().Should().Be(-7);
            reader.ReadU128().Should().Be(UInt128.MaxValue);
            reader.ReadI128().Should().Be((Int128)(-12345));
            reader.ReadBool().Should().BeTrue();
            reader.ReadOption().Should().BeTrue();
            reader.ReadU64().Should().Be(7);
            reader.ReadOption().Should().BeFalse();
            reader.ReadString().Should().Be("hi");
            reader.ReadPublicKey().Should().Be(pubkey);
            reader.ReadBytes(2).ToArray().Should().Equal((byte)0xAA, 0xBB);
            reader.ReadByteVector().Should().Equal((byte)1, 2, 3);
            reader.Remaining.Should().Be(0);
        }

        [Test]
        public void WritesLittleEndian()
        {
            // Arrange
            var writer = new BorshWriter();

            // Act
            writer.WriteU64(1_000_000);

            // Assert
            Convert.ToHexString(writer.ToArray()).ToLowerInvariant().Should().Be("40420f0000000000");
        }

        [Test]
        public void WriteLength_WritesAU32Prefix()
        {
            // Arrange
            var writer = new BorshWriter();

            // Act
            writer.WriteLength(300);

            // Assert
            var reader = new BorshReader(writer.ToArray());
            reader.ReadU32().Should().Be(300u);
            reader.Remaining.Should().Be(0);
        }

        [Test]
        public void WriteLength_NegativeThrows()
        {
            // Arrange
            var writer = new BorshWriter();

            // Act
            Action act = () => writer.WriteLength(-1);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
