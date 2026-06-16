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

            var message = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], [table]);

            const string expected =
                "8002000104010101010101010101010101010101010101010101010101010101010101010106060606060606060606060606060606060606060606060606060606060606060404040404040404040404040404040404040404040404040404040404040404090909090909090909090909090909090909090909090909090909090909090908080808080808080808080808080808080808080808080808080808080808080103040405020102010201050505050505050505050505050505050505050505050505050505050505050501000101";
            Convert.ToHexString(message.Serialize()).ToLowerInvariant().Should().Be(expected);
        }

        // KAT vs solders: same payer/program, no lookup tables -> empty lookup section after the instructions.
        [Test]
        public void WithNoLookupTables_MatchesSolders()
        {
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

            var message = MessageV0.Compile(Pk(1), Blockhash(8), [instruction], []);

            const string expected =
                "8001000103010101010101010101010101010101010101010101010101010101010101010102020202020202020202020202020202020202020202020202020202020202020909090909090909090909090909090909090909090909090909090909090909080808080808080808080808080808080808080808080808080808080808080801020200010c0200000040420f000000000000";
            Convert.ToHexString(message.Serialize()).ToLowerInvariant().Should().Be(expected);
        }
    }
}
