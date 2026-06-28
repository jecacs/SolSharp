using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs.Tests;

public static class TransactionTests
{
    // Reference bytes from solders (Rust solana-sdk): a 1_000_000-lamport System transfer signed by the payer.
    private const string SignedTransferHex =
        "01b033059fc60d833f1027350d31401c321c45b7e54477ae7c2fa0211592a57b35" +
        "92bdea62c63e1173d707a6904197cb25b7087d090d360a7caa6e4ab28da12f0d" +
        "010001038a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c" +
        "0202020202020202020202020202020202020202020202020202020202020202" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0303030303030303030303030303030303030303030303030303030303030303" +
        "01020200010c0200000040420f0000000000";

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static byte[] Fill(byte value) => [.. Enumerable.Repeat(value, PublicKey.Length)];

    private static Transaction BuildTransfer(out Keypair payer)
    {
        payer = Keypair.FromSeed(Fill(1));
        var recipient = new PublicKey(Fill(2));
        var system = PublicKey.Parse(SolanaProgramIds.SystemProgram);

        var transfer = new Instruction
        {
            ProgramId = system,
            Accounts = [AccountMeta.WritableSigner(payer.PublicKey), AccountMeta.Writable(recipient)],
            Data = Hex("0200000040420f0000000000")
        };

        var message = Message.Compile(payer.PublicKey, "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8", [transfer]);
        return Transaction.Create(message);
    }

    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void SystemTransfer_MatchesSolanaSdk()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                // Act
                transaction.Sign(payer);

                // Assert
                transaction.Serialize().Should().Equal(Hex(SignedTransferHex));
                transaction.ToBase64().Should().Be(Convert.ToBase64String(Hex(SignedTransferHex)));
            }
        }

        [Test]
        public void NonRequiredSigner_Throws()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using var stranger = Keypair.Generate();
            using (payer)
            {
                // Act
                Action act = () => transaction.Sign(stranger);

                // Assert
                act.Should().Throw<ArgumentException>();
            }
        }
    }

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void Unsigned_LeavesSignatureSlotZeroed()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                // Act
                var bytes = transaction.Serialize();

                // Assert
                bytes[0].Should().Be(1); // ShortVec(1): one signature slot
                bytes.Skip(1).Take(Transaction.SignatureLength).Should().OnlyContain(b => b == 0);
            }
        }
    }
}
