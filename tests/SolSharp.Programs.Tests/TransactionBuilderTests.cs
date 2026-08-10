using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;

namespace SolSharp.Programs.Tests;

public static class TransactionBuilderTests
{
    private const string Blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";

    // Same signed System transfer as TransactionTests, reproduced through the builder.
    private const string SignedTransferHex =
        "01b033059fc60d833f1027350d31401c321c45b7e54477ae7c2fa0211592a57b35" +
        "92bdea62c63e1173d707a6904197cb25b7087d090d360a7caa6e4ab28da12f0d" +
        "010001038a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c" +
        "0202020202020202020202020202020202020202020202020202020202020202" +
        "0000000000000000000000000000000000000000000000000000000000000000" +
        "0303030303030303030303030303030303030303030303030303030303030303" +
        "01020200010c0200000040420f0000000000";

    private static byte[] Fill(byte value) => [.. Enumerable.Repeat(value, PublicKey.Length)];

    [TestFixture]
    public sealed class Build
    {
        [Test]
        public void SignedTransfer_InfersFeePayerFromSigner_MatchesSolanaSdk()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var recipient = new PublicKey(Fill(2));

            // Act
            var transaction = new TransactionBuilder()
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1_000_000))
                .Build(payer);

            // Assert
            transaction.Serialize().Should().Equal(Convert.FromHexString(SignedTransferHex));
        }

        [Test]
        public void WithoutBlockhash_Throws()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var builder = new TransactionBuilder()
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, new(Fill(2)), 1));
            ISigner[] signers = [payer];

            // Act
            Action act = () => builder.Build(signers);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void WithoutInstructions_Throws()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var builder = new TransactionBuilder().SetRecentBlockhash(Blockhash);
            ISigner[] signers = [payer];

            // Act
            Action act = () => builder.Build(signers);

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void WithoutFeePayerOrSigner_Throws()
        {
            // Arrange
            var builder = new TransactionBuilder()
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(new(Fill(1)), new(Fill(2)), 1));

            // Act
            Action act = () => builder.Build();

            // Assert
            act.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void NullFirstSigner_ThrowsDocumentedArgumentNullException()
        {
            // Arrange
            var builder = new TransactionBuilder()
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(new(Fill(1)), new(Fill(2)), 1));

            // Act
            Action act = () => builder.Build(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("signers");
        }
    }

    [TestFixture]
    public sealed class BuildV0
    {
        // KAT vs solders: a v0 transfer whose recipient is drained into an address lookup table, signed by the payer.
        private const string SignedV0Hex =
            "0172bf724b5847ec43050f991f3f87765831b9dd14b37b6d13470db2b0cc3f5f61041e2c1c90b1b0365c81c888f3984510f822f0bbdde022287615a17108d6a00e80010001028a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c0000000000000000000000000000000000000000000000000000000000000000030303030303030303030303030303030303030303030303030303030303030301010200020c0200000040420f0000000000010505050505050505050505050505050505050505050505050505050505050505010000";

        [Test]
        public void SignedV0Transfer_WithLookupTable_MatchesSolders()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var recipient = new PublicKey(Fill(2));
            var table = new AddressLookupTableAccount(new(Fill(5)), [recipient, new(Fill(7))]);

            // Act
            var transaction = new TransactionBuilder()
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1_000_000))
                .SetAddressLookupTables(table)
                .BuildV0(payer);

            // Assert
            transaction.Serialize().Should().Equal(Convert.FromHexString(SignedV0Hex));
        }
    }

    [TestFixture]
    public sealed class AddInstructions
    {
        [Test]
        public void AppendsInOrder()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var first = SystemProgram.Transfer(payer.PublicKey, new(Fill(2)), 1);
            var second = SystemProgram.Transfer(payer.PublicKey, new(Fill(3)), 2);

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetRecentBlockhash(Blockhash)
                .AddInstructions(first, second)
                .BuildMessage();

            // Assert
            var instructions = message.DecompileInstructions([]);
            instructions.Should().HaveCount(2);
            instructions[0].Data.Should().Equal(first.Data);
            instructions[1].Data.Should().Equal(second.Data);
        }

        [Test]
        public void NullElement_ThrowsDocumentedArgumentNullException()
        {
            // Arrange
            var builder = new TransactionBuilder();

            // Act
            Action act = () => builder.AddInstructions([null!]);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("instructions");
        }
    }

    [TestFixture]
    public sealed class SetRecentBlockhash
    {
        [Test]
        public void TypedHash_BuildsWithSameWireValue()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetRecentBlockhash(new Hash(Blockhash))
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, new(Fill(2)), 1))
                .BuildMessage();

            // Assert
            message.RecentBlockhash.Should().Be(Blockhash);
        }
    }

    [TestFixture]
    public sealed class InputValidation
    {
        [Test]
        public void NullRecentBlockhash_ThrowsAtSetter()
        {
            // Arrange
            var builder = new TransactionBuilder();

            // Act
            Action act = () => builder.SetRecentBlockhash(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("recentBlockhash");
        }

        [Test]
        public void NullLookupTable_ThrowsAtSetter()
        {
            // Arrange
            var builder = new TransactionBuilder();

            // Act
            Action act = () => builder.SetAddressLookupTables([null!]);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .Which.ParamName.Should().Be("lookupTables");
        }
    }

    [TestFixture]
    public sealed class BuildMessageV0
    {
        // The message portion of the solders-KAT'd signed v0 transfer above (the bytes after the
        // 1-signature prefix), so the unsigned compile path is checked against the same reference.
        private const string V0MessageHex =
            "80010001028a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c0000000000000000000000000000000000000000000000000000000000000000030303030303030303030303030303030303030303030303030303030303030301010200020c0200000040420f0000000000010505050505050505050505050505050505050505050505050505050505050505010000";

        [Test]
        public void CompilesTheMessageInsideTheSignedV0Transaction_MatchesSolders()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var recipient = new PublicKey(Fill(2));
            var table = new AddressLookupTableAccount(new(Fill(5)), [recipient, new(Fill(7))]);

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1_000_000))
                .SetAddressLookupTables(table)
                .BuildMessageV0();

            // Assert
            Convert.ToHexString(message.Serialize()).ToLowerInvariant().Should().Be(V0MessageHex);
        }
    }

    [TestFixture]
    public sealed class SetDurableNonce
    {
        [Test]
        public void TypedNonce_BuildsWithSameWireValue()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var nonceAccount = new PublicKey(Fill(5));

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetDurableNonce(nonceAccount, payer.PublicKey, new Hash(Blockhash))
                .BuildMessage();

            // Assert
            message.RecentBlockhash.Should().Be(Blockhash);
        }

        [Test]
        public void WithoutOtherInstructions_BuildsNonceAdvanceOnlyMessage()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var nonceAccount = new PublicKey(Fill(5));

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetDurableNonce(nonceAccount, payer.PublicKey, Blockhash)
                .BuildMessage();

            // Assert
            message.RecentBlockhash.Should().Be(Blockhash);
            var instruction = message.DecompileInstructions([]).Should().ContainSingle().Subject;
            instruction.ProgramId.Should().Be(SystemProgram.ProgramId);
            instruction.Data.Should().Equal(Convert.FromHexString("04000000"));
            instruction.Accounts[0].PublicKey.Should().Be(nonceAccount);
        }

        [Test]
        public void WithoutOtherInstructions_BuildsNonceAdvanceOnlyV0Message()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var nonceAccount = new PublicKey(Fill(5));

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetDurableNonce(nonceAccount, payer.PublicKey, Blockhash)
                .BuildMessageV0();

            // Assert
            message.RecentBlockhash.Should().Be(Blockhash);
            var instruction = message.DecompileInstructions([]).Should().ContainSingle().Subject;
            instruction.ProgramId.Should().Be(SystemProgram.ProgramId);
            instruction.Data.Should().Equal(Convert.FromHexString("04000000"));
            instruction.Accounts[0].PublicKey.Should().Be(nonceAccount);
        }

        [Test]
        public void PrependsAdvanceNonce_AndUsesNonceAsTheBlockhash()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var nonceAccount = new PublicKey(Fill(5));
            var recipient = new PublicKey(Fill(2));

            // Act
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetDurableNonce(nonceAccount, payer.PublicKey, Blockhash)
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1))
                .BuildMessage();

            // Assert: the nonce value anchors the message, and AdvanceNonceAccount runs first.
            message.RecentBlockhash.Should().Be(Blockhash);
            var instructions = message.DecompileInstructions([]);
            instructions.Should().HaveCount(2);
            instructions[0].ProgramId.Should().Be(SystemProgram.ProgramId);
            instructions[0].Data.Should().Equal(Convert.FromHexString("04000000"));
            instructions[0].Accounts[0].PublicKey.Should().Be(nonceAccount);
        }

        [Test]
        public void FollowedBySetRecentBlockhash_DropsTheAdvanceNonceInstruction()
        {
            // Arrange
            using var payer = Keypair.FromSeed(Fill(1));
            var nonceAccount = new PublicKey(Fill(5));
            var recipient = new PublicKey(Fill(2));

            // Act: the builder switches back to blockhash anchoring, so no nonce instruction may survive.
            var message = new TransactionBuilder()
                .SetFeePayer(payer.PublicKey)
                .SetDurableNonce(nonceAccount, payer.PublicKey, "9zMevzTCu2Q7xJYW4wUqcdF9m6E1qrQuHzLC87uCPmGk")
                .SetRecentBlockhash(Blockhash)
                .AddInstruction(SystemProgram.Transfer(payer.PublicKey, recipient, 1))
                .BuildMessage();

            // Assert
            message.RecentBlockhash.Should().Be(Blockhash);
            var instructions = message.DecompileInstructions([]);
            instructions.Should().HaveCount(1);
            instructions[0].Data[..4].Should().Equal(Convert.FromHexString("02000000"));
        }
    }
}
