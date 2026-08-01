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

    private sealed class TestSigner(PublicKey publicKey, byte[]? signature) : ISigner
    {
        public PublicKey PublicKey { get; } = publicKey;

        public int CallCount { get; private set; }

        byte[] ISigner.Sign(ReadOnlySpan<byte> message)
        {
            CallCount++;
            return signature!;
        }
    }

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
            var stranger = Keypair.Generate();
            using (payer)
            using (stranger)
            {
                // Act
                Action act = () => transaction.Sign(stranger);

                // Assert
                act.Should().Throw<ArgumentException>();
            }
        }

        [Test]
        public void LaterNonRequiredSigner_IsRejectedBeforeAnySignerIsCalled()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                var firstSigner = new TestSigner(payer.PublicKey, new byte[Transaction.SignatureLength]);
                var stranger = new TestSigner(new PublicKey(Fill(9)), new byte[Transaction.SignatureLength]);

                // Act
                Action act = () => transaction.Sign(firstSigner, stranger);

                // Assert
                act.Should().Throw<ArgumentException>();
                firstSigner.CallCount.Should().Be(0);
                stranger.CallCount.Should().Be(0);
            }
        }

        [Test]
        public void NullSignerElement_Throws()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                ISigner[] signers = [null!];

                // Act
                Action act = () => transaction.Sign(signers);

                // Assert
                act.Should().Throw<ArgumentNullException>().WithParameterName(nameof(signers));
            }
        }

        [TestCase(63)]
        [TestCase(65)]
        public void InvalidSignatureLength_Throws(int length)
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                var signer = new TestSigner(payer.PublicKey, new byte[length]);

                // Act
                Action act = () => transaction.Sign(signer);

                // Assert
                act.Should().Throw<ArgumentException>().WithMessage("*64-byte*");
            }
        }

        [Test]
        public void NullSignature_Throws()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                var signer = new TestSigner(payer.PublicKey, signature: null);

                // Act
                Action act = () => transaction.Sign(signer);

                // Assert
                act.Should().Throw<ArgumentException>().WithMessage("*64-byte*");
            }
        }

        [Test]
        public void LaterInvalidSignature_DoesNotCommitEarlierSignature()
        {
            // Arrange
            var payer = new PublicKey(Fill(1));
            var second = new PublicKey(Fill(2));
            var instruction = new Instruction
            {
                ProgramId = new PublicKey(Fill(9)),
                Accounts = [AccountMeta.ReadonlySigner(second)],
                Data = [7]
            };
            var message = Message.Compile(payer, new PublicKey(Fill(8)).ToString(), [instruction]);
            var transaction = Transaction.Create(message);
            var firstSigner = new TestSigner(payer, [.. Enumerable.Repeat((byte)0xAB, Transaction.SignatureLength)]);
            var invalidSecondSigner = new TestSigner(second, new byte[Transaction.SignatureLength - 1]);

            // Act
            Action act = () => transaction.Sign(firstSigner, invalidSecondSigner);

            // Assert
            act.Should().Throw<ArgumentException>();
            transaction.Serialize().Skip(1).Take(2 * Transaction.SignatureLength).Should().OnlyContain(value => value == 0);
        }

        [Test]
        public void MutatingReturnedSignature_DoesNotMutateTransaction()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                byte[] signature = [.. Enumerable.Repeat((byte)0xAB, Transaction.SignatureLength)];
                var signer = new TestSigner(payer.PublicKey, signature);
                transaction.Sign(signer);

                // Act
                signature[0] = 0;

                // Assert
                transaction.Serialize()[1].Should().Be(0xAB);
            }
        }

        [Test]
        public void SignerSlotsRemainBoundToCapturedMessageAfterAccountKeyMutation()
        {
            // Arrange
            var payer = new PublicKey(Fill(1));
            var second = new PublicKey(Fill(2));
            var stranger = new PublicKey(Fill(3));
            var instruction = new Instruction
            {
                ProgramId = new PublicKey(Fill(9)),
                Accounts = [AccountMeta.ReadonlySigner(second)],
                Data = [7]
            };
            var message = Message.Compile(payer, new PublicKey(Fill(8)).ToString(), [instruction]);
            var transaction = Transaction.Create(message);
            var signature = new byte[Transaction.SignatureLength];
            var payerSigner = new TestSigner(payer, signature);
            var secondSigner = new TestSigner(second, signature);
            var strangerSigner = new TestSigner(stranger, signature);
            transaction.Sign(payerSigner);

            // Act
            ((List<PublicKey>)message.AccountKeys)[1] = stranger;
            Action strangerAct = () => transaction.Sign(strangerSigner);
            Action originalSignerAct = () => transaction.Sign(secondSigner);

            // Assert
            strangerAct.Should().Throw<ArgumentException>();
            originalSignerAct.Should().NotThrow();
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

        [Test]
        public void SignedTransaction_IsStableAfterMessageMutation()
        {
            // Arrange
            var transaction = BuildTransfer(out var payer);
            using (payer)
            {
                transaction.Sign(payer);
                var before = transaction.Serialize();

                // Act
                transaction.Message.Instructions[0].Data[0] ^= 0xFF;

                // Assert
                transaction.Serialize().Should().Equal(before);
                transaction.GetSerializedLength().Should().Be(before.Length);
            }
        }
    }
}
