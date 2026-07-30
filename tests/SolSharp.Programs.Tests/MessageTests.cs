using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MessageTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class Compile
    {
        // Reference bytes generated with solders (the Rust solana-sdk): a System transfer of 1_000_000
        // lamports from a fixed payer to a fixed recipient.
        [Test]
        public void SystemTransfer_MatchesSolanaSdk()
        {
            // Arrange
            var payer = PublicKey.Parse("AKnL4NNf3DGWZJS6cPknBuEGnVsV4A4m5tgebLHaRSZ9");
            var recipient = PublicKey.Parse("8qbHbw2BbbTHBW1sbeqakYXVKRQM8Ne7pLK7m6CVfeR");
            var system = PublicKey.Parse(SolanaProgramIds.SystemProgram);
            const string blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";

            var transfer = new Instruction
            {
                ProgramId = system,
                Accounts = [AccountMeta.WritableSigner(payer), AccountMeta.Writable(recipient)],
                Data = Hex("0200000040420f0000000000")
            };

            // Act
            var message = Message.Compile(payer, blockhash, [transfer]);

            // Assert
            message.RequiredSignatures.Should().Be(1);
            message.ReadonlySignedAccounts.Should().Be(0);
            message.ReadonlyUnsignedAccounts.Should().Be(1);
            message.AccountKeys.Should().Equal(payer, recipient, system);
            message.Serialize().Should().Equal(Hex(
                "010001038a88e3dd7409f195fd52db2d3cba5d72ca6709bf1d94121bf3748801b40f6f5c" +
                "0202020202020202020202020202020202020202020202020202020202020202" +
                "0000000000000000000000000000000000000000000000000000000000000000" +
                "0303030303030303030303030303030303030303030303030303030303030303" +
                "01020200010c0200000040420f0000000000"));
        }

        // Reference bytes from solders for a case that exercises dedup, flag merging (an account that is
        // read-only in one instruction and writable in another becomes writable), all four account
        // classes, and the by-public-key ordering within each class.
        [Test]
        public void DedupMergeAndOrdering_MatchesSolanaSdk()
        {
            // Arrange
            PublicKey payer = Key(1), readonlySigner = Key(2), writableNonSigner = Key(3);
            PublicKey readonlyNonSigner = Key(4), shared = Key(5), programOne = Key(10), programTwo = Key(11);
            const string blockhash = "CktRuQ2mttgRGkXJtyksdKHjUdc2C4TgDzyB98oEzy8";

            var first = new Instruction
            {
                ProgramId = programOne,
                Accounts =
                [
                    AccountMeta.WritableSigner(payer),
                    AccountMeta.ReadonlySigner(readonlySigner),
                    AccountMeta.Readonly(shared),
                    AccountMeta.Readonly(readonlyNonSigner)
                ],
                Data = [0x01, 0x02]
            };
            var second = new Instruction
            {
                ProgramId = programTwo,
                Accounts = [AccountMeta.Writable(writableNonSigner), AccountMeta.Writable(shared)],
                Data = [0xAA]
            };

            // Act
            var message = Message.Compile(payer, blockhash, [first, second]);

            // Assert
            message.RequiredSignatures.Should().Be(2);
            message.ReadonlySignedAccounts.Should().Be(1);
            message.ReadonlyUnsignedAccounts.Should().Be(3);
            message.AccountKeys.Should().Equal(
                payer, readonlySigner, writableNonSigner, shared, readonlyNonSigner, programOne, programTwo);
            message.Serialize().Should().Equal(Hex(
                "02010307" +
                "0101010101010101010101010101010101010101010101010101010101010101" +
                "0202020202020202020202020202020202020202020202020202020202020202" +
                "0303030303030303030303030303030303030303030303030303030303030303" +
                "0505050505050505050505050505050505050505050505050505050505050505" +
                "0404040404040404040404040404040404040404040404040404040404040404" +
                "0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a" +
                "0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b0b" +
                "0303030303030303030303030303030303030303030303030303030303030303" +
                "020504000103040201020602020301aa"));
        }
    }

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void InvalidBlockhash_Throws()
        {
            // Arrange
            var system = PublicKey.Parse(SolanaProgramIds.SystemProgram);
            var instruction = new Instruction { ProgramId = system, Accounts = [], Data = [] };
            var message = Message.Compile(Key(1), "not-a-blockhash", [instruction]);

            // Act
            Action act = () => message.Serialize();

            // Assert
            act.Should().Throw<FormatException>();
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
        // Wire offsets in SerializedTransfer(): the 3 header bytes, the key count, three 32-byte keys,
        // and the blockhash put the instruction's program id index at 133 and its account indexes at
        // 135 (the payer) and 136 (the recipient).
        private const int ProgramIdIndexOffset = 133;
        private const int SecondAccountIndexOffset = 136;

        // Keys [Key(1), Key(2), Key(9)], header (1, 0, 1), one instruction: program index 2, accounts [0, 1].
        private static byte[] SerializedTransfer()
        {
            var instruction = new Instruction
            {
                ProgramId = Key(9),
                Accounts = [AccountMeta.WritableSigner(Key(1)), AccountMeta.Writable(Key(2))],
                Data = [7]
            };
            return Message.Compile(Key(1), Key(8).ToString(), [instruction]).Serialize();
        }

        [Test]
        public void TruncatedData_ThrowsFormatException()
        {
            // Arrange: a compiled transfer cut in the middle of the account keys.
            var system = PublicKey.Parse(SolanaProgramIds.SystemProgram);
            var instruction = new Instruction { ProgramId = system, Accounts = [AccountMeta.Writable(Key(2))], Data = [7] };
            var data = Message.Compile(Key(1), Key(8).ToString(), [instruction]).Serialize()[..6];

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void ValidCompiledMessage_RoundTrips()
        {
            // Arrange
            var data = SerializedTransfer();

            // Act
            var message = Message.Deserialize(data);

            // Assert
            message.Serialize().Should().Equal(data);
        }

        [Test]
        public void HeaderAreasOverlapAccountKeys_ThrowsFormatException()
        {
            // Arrange: 1 signer plus 3 read-only unsigned accounts cannot fit the 3 account keys.
            var data = SerializedTransfer();
            data[2] = 3;

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*only 3 account key(s)*");
        }

        // Solana requires readonlySignedAccounts < requiredSignatures so at least one signer - the fee
        // payer - stays writable; (0, 0) additionally covers a message demanding no signatures at all.
        [TestCase((byte)1, (byte)1)]
        [TestCase((byte)0, (byte)0)]
        public void NoWritableFeePayerSigner_ThrowsFormatException(byte requiredSignatures, byte readonlySigned)
        {
            // Arrange
            var data = SerializedTransfer();
            data[0] = requiredSignatures;
            data[1] = readonlySigned;

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*fee payer must be a writable signer*");
        }

        [Test]
        public void ProgramIdIndexOutOfRange_ThrowsFormatException()
        {
            // Arrange: point the instruction's program id past the 3 account keys.
            var data = SerializedTransfer();
            data[ProgramIdIndexOffset] = 3;

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*program id index 3*");
        }

        [Test]
        public void ProgramIdIndexIsFeePayer_ThrowsFormatException()
        {
            // Arrange: point the instruction's program id at account 0, the fee payer.
            var data = SerializedTransfer();
            data[ProgramIdIndexOffset] = 0;

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*a program cannot be the fee payer*");
        }

        [Test]
        public void AccountIndexOutOfRange_ThrowsFormatException()
        {
            // Arrange: point the instruction's second account past the 3 account keys.
            var data = SerializedTransfer();
            data[SecondAccountIndexOffset] = 3;

            // Act
            Action act = () => Message.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*account index 3*");
        }
    }
}
