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
            var pubkey = new PublicKey(Enumerable.Repeat((byte)9, PublicKey.Length).ToArray());

            var writer = new BorshWriter();
            writer.WriteU8(42);
            writer.WriteI8(-5);
            writer.WriteU16(0x1234);
            writer.WriteU32(0x12345678);
            writer.WriteU64(1_000_000);
            writer.WriteI64(-7);
            writer.WriteBool(true);
            writer.WriteOption(true);
            writer.WriteU64(7);
            writer.WriteOption(false);
            writer.WriteString("hi");
            writer.WritePublicKey(pubkey);
            writer.WriteByteVector([1, 2, 3]);

            var reader = new BorshReader(writer.ToArray());
            reader.ReadU8().Should().Be(42);
            reader.ReadI8().Should().Be(-5);
            reader.ReadU16().Should().Be(0x1234);
            reader.ReadU32().Should().Be(0x12345678);
            reader.ReadU64().Should().Be(1_000_000);
            reader.ReadI64().Should().Be(-7);
            reader.ReadBool().Should().BeTrue();
            reader.ReadOption().Should().BeTrue();
            reader.ReadU64().Should().Be(7);
            reader.ReadOption().Should().BeFalse();
            reader.ReadString().Should().Be("hi");
            reader.ReadPublicKey().Should().Be(pubkey);
            reader.ReadByteVector().Should().Equal((byte)1, 2, 3);
            reader.Remaining.Should().Be(0);
        }

        [Test]
        public void WritesLittleEndian()
        {
            var writer = new BorshWriter();
            writer.WriteU64(1_000_000);

            Convert.ToHexString(writer.ToArray()).ToLowerInvariant().Should().Be("40420f0000000000");
        }
    }
}
