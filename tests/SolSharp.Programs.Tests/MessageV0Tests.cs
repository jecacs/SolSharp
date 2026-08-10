using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MessageV0Tests
{
    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new(bytes);
    }

    private static PublicKey UniqueKey(int value)
    {
        var bytes = new byte[PublicKey.Length];
        bytes[0] = (byte)value;
        bytes[1] = (byte)(value >> 8);
        return new(bytes);
    }

    // 32 bytes encode to the same base58 whether they represent a key or a blockhash.
    private static string Blockhash(byte value) => Pk(value).ToString();

    [TestFixture]
    public sealed class Compile
    {
        // KAT vs solders: MessageV0.try_compile(payer=[1], [ix], [alt], blockhash=[8]) -> to_bytes_versioned.
        // ix(program=[9], data=0102): A[2] writable, B[3] readonly, C[4] writable, D[6] writable signer.
        // alt=[5] holds [A, B, [7]] -> A drains writable (index 0), B drains readonly (index 1).
        [Test]
        public void DrainsAccountsIntoLookupTable_MatchesSolders()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Writable(Pk(2)),
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.WritableSigner(Pk(6))
                ],
                Data = [1, 2]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2), Pk(3), Pk(7)]);

            // Act
            var message = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], [table]);

            // Assert
            const string expected =
                "8002000104010101010101010101010101010101010101010101010101010101010101010106060606060606060606060606060606060606060606060606060606060606060404040404040404040404040404040404040404040404040404040404040404090909090909090909090909090909090909090909090909090909090909090908080808080808080808080808080808080808080808080808080808080808080103040405020102010201050505050505050505050505050505050505050505050505050505050505050501000101";
            Convert.ToHexString(message.Serialize()).ToLowerInvariant().Should().Be(expected);
        }

        // KAT vs solders: same payer/program, no lookup tables -> empty lookup section after the instructions.
        [Test]
        public void WithNoLookupTables_MatchesSolders()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.WritableSigner(Pk(1)),
                    AccountMeta.Writable(Pk(2))
                ],
                Data = [2, 0, 0, 0, 0x40, 0x42, 0x0f, 0, 0, 0, 0, 0]
            };

            // Act
            var message = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], []);

            // Assert
            const string expected =
                "8001000103010101010101010101010101010101010101010101010101010101010101010102020202020202020202020202020202020202020202020202020202020202020909090909090909090909090909090909090909090909090909090909090909080808080808080808080808080808080808080808080808080808080808080801020200010c0200000040420f000000000000";
            Convert.ToHexString(message.Serialize()).ToLowerInvariant().Should().Be(expected);
        }

        [Test]
        public void TypedBlockhash_MatchesStringOverload()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [7] };
            var blockhash = new Hash(Pk(8).ToBytes());

            // Act
            var typed = MessageV0.Compile(Pk(1), blockhash, [instruction], []);
            var text = MessageV0.Compile(Pk(1), blockhash.ToString(), [instruction], []);

            // Assert
            typed.Serialize().Should().Equal(text.Serialize());
        }

        [Test]
        public void OversizedLookupTable_Throws()
        {
            // Arrange: 257 addresses cannot be addressed by the single-byte wire indexes.
            var addresses = new PublicKey[MessageV0.MaxAccounts + 1];
            for (var i = 0; i < addresses.Length; i++)
            {
                var bytes = new byte[PublicKey.Length];
                bytes[0] = (byte)i;
                bytes[1] = (byte)(i >> 8);
                addresses[i] = new(bytes);
            }

            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [AccountMeta.Writable(Pk(2))], Data = [] };
            var table = new AddressLookupTableAccount(Pk(5), addresses);

            // Act
            Action act = () => MessageV0.Compile(Pk(1), Blockhash(8), [instruction], [table]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 256*");
        }

        [Test]
        public void TwoHundredFiftyFiveSigners_Compiles()
        {
            // Arrange
            var instruction = AllSigners(byte.MaxValue, out var payer);

            // Act
            var message = MessageV0.Compile(payer, Blockhash(8), [instruction], []);

            // Assert
            message.RequiredSignatures.Should().Be(byte.MaxValue);
        }

        [Test]
        public void TwoHundredFiftySixSigners_ThrowsInsteadOfWrapping()
        {
            // Arrange
            var instruction = AllSigners(MessageV0.MaxAccounts, out var payer);

            // Act
            Action act = () => MessageV0.Compile(payer, Blockhash(8), [instruction], []);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 255 signatures*");
        }

        [Test]
        public void DurableNoncePresentInLookup_RemainsStatic()
        {
            // Arrange
            var payer = Pk(1);
            var nonce = Pk(2);
            var advance = SystemProgram.AdvanceNonceAccount(nonce, payer);
            var table = new AddressLookupTableAccount(Pk(5), [nonce]);

            // Act
            var message = MessageV0.Compile(payer, Blockhash(8), [advance], [table]);

            // Assert
            message.AccountKeys.Should().Contain(nonce);
            message.AddressTableLookups.Should().BeEmpty();
        }

        [Test]
        public void AdvanceNoncePrefixWithTrailingData_RemainsStatic_MatchingUpstream()
        {
            // Arrange
            var payer = Pk(1);
            var nonce = Pk(2);
            var canonicalAdvance = SystemProgram.AdvanceNonceAccount(nonce, payer);
            var advance = new Instruction
            {
                ProgramId = canonicalAdvance.ProgramId,
                Accounts = canonicalAdvance.Accounts,
                Data = [.. canonicalAdvance.Data, 0xAA]
            };
            var table = new AddressLookupTableAccount(Pk(5), [nonce]);

            // Act
            var message = MessageV0.Compile(payer, Blockhash(8), [advance], [table]);

            // Assert
            message.AccountKeys.Should().Contain(nonce);
            message.AddressTableLookups.Should().BeEmpty();
        }

        [Test]
        public void NonFirstAdvanceNonce_DoesNotPinAccountStatic()
        {
            // Arrange
            var payer = Pk(1);
            var nonce = Pk(2);
            var first = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [] };
            var advance = SystemProgram.AdvanceNonceAccount(nonce, payer);
            var table = new AddressLookupTableAccount(Pk(5), [nonce]);

            // Act
            var message = MessageV0.Compile(payer, Blockhash(8), [first, advance], [table]);

            // Assert
            message.AccountKeys.Should().NotContain(nonce);
            message.AddressTableLookups.Should().ContainSingle();
        }

        [Test]
        public void MutatingSourceData_DoesNotMutateCompiledMessage()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [7] };
            var message = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], []);

            // Act
            instruction.Data[0] = 99;

            // Assert
            message.Instructions[0].Data.Should().Equal(7);
        }

        private static Instruction AllSigners(int count, out PublicKey payer)
        {
            var keys = Enumerable.Range(0, count).Select(UniqueKey).ToArray();
            payer = keys[0];
            return new()
            {
                ProgramId = keys[^1],
                Accounts = [.. keys.Skip(1).Select(AccountMeta.ReadonlySigner)],
                Data = []
            };
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
        // Wire offsets in SerializedV0(): the version prefix, the 3 header bytes, the key count, three
        // 32-byte keys, and the blockhash put the instruction's program id index at 134 and its account
        // indexes at 136 (the payer) and 137 (the recipient). The final byte is the lookup count (0).
        private const int ProgramIdIndexOffset = 134;
        private const int SecondAccountIndexOffset = 137;

        [Test]
        public void UnsupportedVersion_Throws()
        {
            // Arrange: flip the version prefix from v0 (0x80) to a hypothetical v1 (0x81).
            var data = SerializedV0();
            data[0] = 0x81;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*version 1*");
        }

        [Test]
        public void TruncatedData_ThrowsFormatException()
        {
            // Arrange: cut the serialized message in the middle of the account keys.
            var data = SerializedV0()[..10];

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void ImpossibleLookupCount_ThrowsBeforeAllocatingDeclaredArray()
        {
            // Arrange: replace the zero lookup count with max compact-u16, without adding lookup data.
            byte[] data = [.. SerializedV0()[..^1], 0xff, 0xff, 0x03];

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*declares 65535 address table lookup(s)*");
        }

        [Test]
        public void ValidCompiledMessage_RoundTrips()
        {
            // Arrange
            var data = SerializedV0();

            // Act
            var message = MessageV0.Deserialize(data);

            // Assert
            message.Serialize().Should().Equal(data);
        }

        [Test]
        public void ValidMessageWithLookup_RoundTrips()
        {
            // Arrange: the drained-lookup message KAT'd against solders in the Compile fixture.
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Writable(Pk(2)),
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.WritableSigner(Pk(6))
                ],
                Data = [1, 2]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2), Pk(3), Pk(7)]);
            var data = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], [table]).Serialize();

            // Act
            var message = MessageV0.Deserialize(data);

            // Assert
            message.Serialize().Should().Equal(data);
        }

        [Test]
        public void HeaderAreasOverlapAccountKeys_ThrowsFormatException()
        {
            // Arrange: 1 signer plus 3 read-only unsigned accounts cannot fit the 3 static keys.
            var data = SerializedV0();
            data[3] = 3;

            // Act
            Action act = () => MessageV0.Deserialize(data);

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
            var data = SerializedV0();
            data[1] = requiredSignatures;
            data[2] = readonlySigned;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*fee payer must be a writable signer*");
        }

        [Test]
        public void NoStaticAccountKeys_ThrowsFormatException()
        {
            // Arrange: a degenerate v0 message - all-zero header, no keys, no instructions, no lookups.
            // The fee-payer rule is what rejects it, exactly as in Solana's sanitize.
            byte[] data = [MessageV0.VersionPrefix, 0, 0, 0, 0, .. new byte[32], 0, 0];

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*fee payer must be a writable signer*");
        }

        [Test]
        public void LookupLoadsNoAccounts_ThrowsFormatException()
        {
            // Arrange: append a lookup for table Pk(5) with zero writable and zero readonly indexes.
            var data = SerializedV0();
            data[^1] = 1;
            byte[] corrupted = [.. data, .. Pk(5).ToBytes(), 0, 0];

            // Act
            Action act = () => MessageV0.Deserialize(corrupted);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*loads no accounts*");
        }

        [Test]
        public void MoreAddressableAccountsThanMax_ThrowsFormatException()
        {
            // Arrange: a lookup loading 127 writable + 127 readonly accounts pushes the total to
            // 3 static + 254 loaded = 257; the table-local index values are irrelevant to the cap.
            var data = SerializedV0();
            data[^1] = 1;
            byte[] corrupted = [.. data, .. Pk(5).ToBytes(), 127, .. new byte[127], 127, .. new byte[127]];

            // Act
            Action act = () => MessageV0.Deserialize(corrupted);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*257 accounts*at most 256*");
        }

        [Test]
        public void AddressableAccountsAtMax_Deserializes()
        {
            // Arrange: 3 static + 127 writable + 126 readonly = exactly 256 addressable accounts.
            var data = SerializedV0();
            data[^1] = 1;
            byte[] extended = [.. data, .. Pk(5).ToBytes(), 127, .. new byte[127], 126, .. new byte[126]];

            // Act
            var message = MessageV0.Deserialize(extended);

            // Assert
            message.AddressTableLookups.Should().ContainSingle();
            message.AddressTableLookups[0].WritableIndexes.Should().HaveCount(127);
            message.AddressTableLookups[0].ReadonlyIndexes.Should().HaveCount(126);
        }

        [Test]
        public void ProgramIdIndexOutOfRange_ThrowsFormatException()
        {
            // Arrange: point the instruction's program id past the 3 account keys.
            var data = SerializedV0();
            data[ProgramIdIndexOffset] = 3;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*program id index 3*");
        }

        [Test]
        public void ProgramIdIndexInLookupRange_ThrowsFormatException()
        {
            // Arrange: with one lookup-loaded account the message addresses 4 accounts, but Solana still
            // rejects program id index 3 - a program id must be a static key, never lookup-loaded.
            var data = WithSingleAccountLookup(SerializedV0());
            data[ProgramIdIndexOffset] = 3;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*program id index 3*");
        }

        [Test]
        public void ProgramIdIndexIsFeePayer_ThrowsFormatException()
        {
            // Arrange: point the instruction's program id at account 0, the fee payer.
            var data = SerializedV0();
            data[ProgramIdIndexOffset] = 0;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*a program cannot be the fee payer*");
        }

        [Test]
        public void AccountIndexOutOfRange_ThrowsFormatException()
        {
            // Arrange: point the instruction's second account past the 3 addressable accounts.
            var data = SerializedV0();
            data[SecondAccountIndexOffset] = 3;

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*account index 3*");
        }

        [Test]
        public void AccountIndexInLookupRange_Deserializes()
        {
            // Arrange: with one lookup-loaded account, instruction account index 3 is addressable.
            var data = WithSingleAccountLookup(SerializedV0());
            data[SecondAccountIndexOffset] = 3;

            // Act
            var message = MessageV0.Deserialize(data);

            // Assert
            message.Instructions[0].AccountIndexes.Should().Equal(0, 3);
        }

        [Test]
        public void TrailingByte_ThrowsFormatException()
        {
            // Arrange
            byte[] data = [.. SerializedV0(), 0xAA];

            // Act
            Action act = () => MessageV0.Deserialize(data);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*1 trailing byte(s)*");
        }

        // Appends a lookup that loads one writable account from table Pk(5), making 4 addressable
        // accounts: the 3 static keys plus one lookup-loaded key at index 3.
        private static byte[] WithSingleAccountLookup(byte[] data)
        {
            data[^1] = 1;
            return [.. data, .. Pk(5).ToBytes(), 1, 0, 0];
        }

        private static byte[] SerializedV0()
        {
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.WritableSigner(Pk(1)), AccountMeta.Writable(Pk(2))],
                Data = [1, 2, 3]
            };
            return MessageV0.Compile(Pk(1), Blockhash(8), [instruction], []).Serialize();
        }
    }
}
