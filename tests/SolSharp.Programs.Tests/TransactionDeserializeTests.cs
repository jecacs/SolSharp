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
            var bytes = Convert.FromHexString(SignedTransferHex);

            var transaction = Transaction.Deserialize(bytes);

            transaction.Serialize().Should().Equal(bytes);
            transaction.Message.Should().BeOfType<Message>();
            transaction.Message.RequiredSignatures.Should().Be(1);
            transaction.Message.AccountKeys.Should().HaveCount(3);
        }

        [Test]
        public void V0Transfer_RoundTripsAndIsVersioned()
        {
            var bytes = Convert.FromHexString(SignedV0Hex);

            var transaction = Transaction.Deserialize(bytes);

            transaction.Serialize().Should().Equal(bytes);
            transaction.Message.Should().BeOfType<MessageV0>();

            var message = (MessageV0)transaction.Message;
            message.AddressTableLookups.Should().ContainSingle();
            message.AddressTableLookups[0].WritableIndexes.Should().Equal((byte)0);
        }
    }
}
