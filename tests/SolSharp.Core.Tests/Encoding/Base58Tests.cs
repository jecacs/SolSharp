using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Encoding;

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
            var decoded = Base58.Decode(SolanaProgramIds.TokenProgram);

            decoded.Should().HaveCount(32);
            Base58.Encode(decoded).Should().Be(SolanaProgramIds.TokenProgram);
        }

        [Test]
        public void RandomBytes_RoundTrip()
        {
            var rng = new Random(1234);

            for (var i = 0; i < 500; i++)
            {
                var bytes = new byte[rng.Next(0, 64)];
                rng.NextBytes(bytes);

                Base58.Decode(Base58.Encode(bytes)).Should().Equal(bytes);
            }
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
}
