using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Programs.Tests;

public static class TransactionDeserializeTests
{
    // The signed legacy System transfer KAT'd in TransactionTests (from solders).
    private const string SignedTransferHex =
        "01b033059fc60d833f1027350d31401c321c45b7e54477ae7c2fa0211592a57b35" +
        "92bdea62c63e1173d707a6904197cb25b7087d090d360a7caa6e4ab28da12f0d" +
        "010001038a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c" +
        "0202020202020202020202020202020202020202020202020202020202020202" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0303030303030303030303030303030303030303030303030303030303030303" +
        "01020200010c0200000040420f0000000000";

    // The signed v0 transfer (with an address lookup table) KAT'd in TransactionBuilderTests (from solders).
    private const string SignedV0Hex =
        "0172bf724b5847ec43050f991f3f87765831b9dd14b37b6d13470db2b0cc3f5f61041e2c1c90b1b0365c81c888f3984510f822f0bbdde022287615a17108d6a00e80010001028a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c0000000000000000000000000000000000000000000000000000000000000000030303030303030303030303030303030303030303030303030303030303030301010200020c0200000040420f0000000000010505050505050505050505050505050505050505050505050505050505050505010000";

    [TestFixture]
    public sealed class Deserialize
    {
        [Test]
        public void LegacyTransfer_RoundTripsAndParsesFields()
        {
            // Arrange
            var bytes = Convert.FromHexString(SignedTransferHex);

            // Act
            var transaction = Transaction.Deserialize(bytes);

            // Assert
            transaction.Serialize().Should().Equal(bytes);
            transaction.Message.Should().BeOfType<Message>();
            transaction.Message.RequiredSignatures.Should().Be(1);
            transaction.Message.AccountKeys.Should().HaveCount(3);
        }

        [Test]
        public void V0Transfer_RoundTripsAndIsVersioned()
        {
            // Arrange
            var bytes = Convert.FromHexString(SignedV0Hex);

            // Act
            var transaction = Transaction.Deserialize(bytes);

            // Assert
            transaction.Serialize().Should().Equal(bytes);
            transaction.Message.Should().BeOfType<MessageV0>();

            var message = (MessageV0)transaction.Message;
            message.AddressTableLookups.Should().ContainSingle();
            message.AddressTableLookups[0].WritableIndexes.Should().Equal(0);
        }

        [TestCase(SignedTransferHex)]
        [TestCase(SignedV0Hex)]
        public void MessageMutation_DoesNotChangeReserializedBytes(string hex)
        {
            // Arrange
            var bytes = Convert.FromHexString(hex);
            var transaction = Transaction.Deserialize(bytes);

            // Act
            transaction.Message.Instructions[0].Data[0] ^= 0xFF;

            // Assert
            transaction.Serialize().Should().Equal(bytes);
        }

        [Test]
        public void TruncatedData_ThrowsFormatException()
        {
            // Arrange: cut inside the first signature.
            var truncated = Convert.FromHexString(SignedTransferHex)[..10];

            // Act
            Action act = () => Transaction.Deserialize(truncated);

            // Assert
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void ImpossibleSignatureCount_ThrowsBeforeAllocatingDeclaredArray()
        {
            // Arrange: max compact-u16 signature count with no signatures or message.
            byte[] data = [0xff, 0xff, 0x03];

            // Act
            Action act = () => Transaction.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*declares 65535 signature slot(s)*");
        }

        [Test]
        public void FewerSignatureSlotsThanRequiredSigners_ThrowsFormatException()
        {
            // Arrange: rewrite the signature count to 0 and drop the 64 signature bytes, leaving a message
            // that still requires one signer - the shape Solana's sanitize step rejects.
            var signed = Convert.FromHexString(SignedTransferHex);
            var tampered = new byte[signed.Length - 64];
            tampered[0] = 0;
            signed.AsSpan(1 + 64).CopyTo(tampered.AsSpan(1));

            // Act
            Action act = () => Transaction.Deserialize(tampered);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*0 signature slot(s)*requires 1*");
        }

        [Test]
        public void MoreSignatureSlotsThanRequiredSigners_ThrowsFormatException()
        {
            // Arrange: rewrite the signature count to 2 and insert a second zeroed signature.
            var signed = Convert.FromHexString(SignedTransferHex);
            var tampered = new byte[signed.Length + 64];
            tampered[0] = 2;
            signed.AsSpan(1, 64).CopyTo(tampered.AsSpan(1));
            signed.AsSpan(1 + 64).CopyTo(tampered.AsSpan(1 + 128));

            // Act
            Action act = () => Transaction.Deserialize(tampered);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*2 signature slot(s)*requires 1*");
        }

        [TestCase(SignedTransferHex)]
        [TestCase(SignedV0Hex)]
        public void TrySerialize_MatchesSerializeAndReportsExactLength(string hex)
        {
            // Arrange
            var bytes = Convert.FromHexString(hex);
            var transaction = Transaction.Deserialize(bytes);

            // Act
            var buffer = new byte[transaction.GetSerializedLength()];
            var ok = transaction.TrySerialize(buffer, out var written);

            // Assert: the span path emits the same bytes as the allocating path, sized exactly.
            ok.Should().BeTrue();
            written.Should().Be(bytes.Length);
            buffer.Should().Equal(bytes);
        }

        [Test]
        public void TrySerialize_TooSmallBuffer_ReturnsFalse()
        {
            // Arrange
            var transaction = Transaction.Deserialize(Convert.FromHexString(SignedTransferHex));

            // Act
            var ok = transaction.TrySerialize(new byte[10], out var written);

            // Assert
            ok.Should().BeFalse();
            written.Should().Be(0);
        }

        [TestCase(SignedTransferHex)]
        [TestCase(SignedV0Hex)]
        public void TrailingByte_ThrowsFormatException(string hex)
        {
            // Arrange
            byte[] bytes = [.. Convert.FromHexString(hex), 0xAA];

            // Act
            Action act = () => Transaction.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*trailing byte(s)*");
        }
    }
}
