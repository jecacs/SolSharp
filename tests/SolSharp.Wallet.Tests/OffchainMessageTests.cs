using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class OffchainMessageTests
{
    private const string UpstreamAsciiHash = "HG5JydBGjtjTfD3sSn21ys5NTWPpXzmqifiGC2BVUjkD";
    private const string UpstreamUtf8Hash = "6GXTveatZQLexkX4WeTpJ3E7uk1UojRXpKp43c4ArSun";
    private static readonly byte[] UpstreamAsciiWire = Convert.FromHexString(
        "FF736F6C616E61206F6666636861696E00000C0054657374204D657373616765");

    private static readonly byte[] UpstreamUtf8Wire = Convert.FromHexString(
        "FF736F6C616E61206F6666636861696E00012300D0A2D0B5D181D182D0BED0B2"
        + "D0BED0B520D181D0BED0BED0B1D189D0B5D0BDD0B8D0B5");

    [TestFixture]
    public sealed class Create
    {
        [Test]
        public void PrintableAscii_UsesRestrictedFormatAndExactUpstreamVector()
        {
            // Act
            var message = OffchainMessage.Create("Test Message");

            // Assert
            message.Version.Should().Be(0);
            message.Format.Should().Be(OffchainMessageFormat.RestrictedAscii);
            message.MessageLength.Should().Be(12);
            message.Serialize().Should().Equal(UpstreamAsciiWire);
            message.ComputeHash().Should().Be(Hash.Parse(UpstreamAsciiHash));
        }

        [Test]
        public void Utf8_UsesLimitedFormatAndExactUpstreamHash()
        {
            // Act
            var message = OffchainMessage.Create("Тестовое сообщение");

            // Assert
            message.Format.Should().Be(OffchainMessageFormat.LimitedUtf8);
            message.Serialize().Should().Equal(UpstreamUtf8Wire);
            message.ComputeHash().Should().Be(Hash.Parse(UpstreamUtf8Hash));
        }

        [Test]
        public void AboveLedgerLimit_UsesExtendedUtf8()
        {
            // Arrange
            var bytes = Enumerable.Repeat((byte)'a', OffchainMessage.MaxLedgerMessageLength + 1).ToArray();

            // Act & Assert
            OffchainMessage.Create(bytes).Format.Should().Be(OffchainMessageFormat.ExtendedUtf8);
        }

        [Test]
        public void ControlCharacter_UsesLimitedUtf8()
            => OffchainMessage.Create("line\n").Format.Should().Be(OffchainMessageFormat.LimitedUtf8);

        [Test]
        public void EmptyOrInvalidUtf8_Throws()
        {
            // Act
            var empty = () => _ = OffchainMessage.Create((byte[])[]);
            var invalid = () => _ = OffchainMessage.Create([0xFF]);

            // Assert
            empty.Should().Throw<ArgumentException>();
            invalid.Should().Throw<ArgumentException>();
        }

        [Test]
        public void UnsupportedVersionOrOversizedPayload_Throws()
        {
            // Act
            var version = () => _ = OffchainMessage.Create(1, "x"u8);
            var oversized = () => _ = OffchainMessage.Create(new byte[OffchainMessage.MaxMessageLength + 1]);

            // Assert
            version.Should().Throw<ArgumentOutOfRangeException>();
            oversized.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
        [Test]
        public void UpstreamVector_RoundTripsAndCopiesPayload()
        {
            // Act
            var message = OffchainMessage.Deserialize(UpstreamAsciiWire);
            var payload = message.ToMessageBytes();
            payload[0] ^= byte.MaxValue;

            // Assert
            message.Serialize().Should().Equal(UpstreamAsciiWire);
            message.ToMessageBytes().Should().Equal("Test Message"u8.ToArray());
            message.Should().Be(OffchainMessage.Create("Test Message"));
        }

        [Test]
        public void InvalidDomainVersionLengthFormatOrPayload_Throws()
        {
            // Arrange
            var invalidDomain = UpstreamAsciiWire.ToArray();
            invalidDomain[0] = 0;
            var invalidVersion = UpstreamAsciiWire.ToArray();
            invalidVersion[16] = 1;
            var invalidLength = UpstreamAsciiWire.ToArray();
            invalidLength[18] = 11;
            var invalidFormat = UpstreamAsciiWire.ToArray();
            invalidFormat[17] = 3;
            var invalidPayload = UpstreamAsciiWire.ToArray();
            invalidPayload[20] = 0;

            // Act & Assert
            ((Action)(() => _ = OffchainMessage.Deserialize(invalidDomain))).Should().Throw<FormatException>();
            ((Action)(() => _ = OffchainMessage.Deserialize(invalidVersion))).Should().Throw<FormatException>();
            ((Action)(() => _ = OffchainMessage.Deserialize(invalidLength))).Should().Throw<FormatException>();
            ((Action)(() => _ = OffchainMessage.Deserialize(invalidFormat))).Should().Throw<FormatException>();
            ((Action)(() => _ = OffchainMessage.Deserialize(invalidPayload))).Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class SignAndVerify
    {
        [Test]
        public void ExactSerializedMessage_IsSignedAndStrictlyVerified()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Convert.FromHexString(
                "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60"));
            var message = OffchainMessage.Create("Test Message");

            // Act
            var signature = message.Sign(keypair);

            // Assert
            signature.Should().Be(keypair.SignSignature(UpstreamAsciiWire));
            message.Verify(keypair.PublicKey, signature).Should().BeTrue();
            OffchainMessage.Create("Other Message").Verify(keypair.PublicKey, signature).Should().BeFalse();
        }
    }
}
