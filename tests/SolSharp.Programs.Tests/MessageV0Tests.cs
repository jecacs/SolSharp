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
        return new PublicKey(bytes);
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
        public void OversizedLookupTable_Throws()
        {
            // Arrange: 257 addresses cannot be addressed by the single-byte wire indexes.
            var addresses = new PublicKey[MessageV0.MaxAccounts + 1];
            for (var i = 0; i < addresses.Length; i++)
            {
                var bytes = new byte[PublicKey.Length];
                bytes[0] = (byte)i;
                bytes[1] = (byte)(i >> 8);
                addresses[i] = new PublicKey(bytes);
            }

            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [AccountMeta.Writable(Pk(2))], Data = [] };
            var table = new AddressLookupTableAccount(Pk(5), addresses);

            // Act
            Action act = () => MessageV0.Compile(Pk(1), Blockhash(8), [instruction], [table]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 256*");
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
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
    }
}
