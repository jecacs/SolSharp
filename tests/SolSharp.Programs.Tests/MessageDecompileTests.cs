using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MessageDecompileTests
{
    private static PublicKey Pk(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable))];

    [TestFixture]
    public sealed class Legacy
    {
        // Compile then decompile must round-trip an instruction touching all four account classes.
        [Test]
        public void ReproducesAllFourAccountClasses()
        {
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.WritableSigner(Pk(1)),
                    AccountMeta.ReadonlySigner(Pk(2)),
                    AccountMeta.Writable(Pk(3)),
                    AccountMeta.Readonly(Pk(4))
                ],
                Data = [7]
            };

            var message = Message.Compile(Pk(1), Pk(8).ToString(), [instruction]);

            var decompiled = message.DecompileInstructions([]).Should().ContainSingle().Subject;
            decompiled.ProgramId.Should().Be(Pk(9));
            decompiled.Data.Should().Equal((byte)7);
            Metas(decompiled).Should().Equal(
                (Pk(1), true, true),
                (Pk(2), true, false),
                (Pk(3), false, true),
                (Pk(4), false, false));

            // The parameterless default (via the interface) works for a message with no lookup tables.
            ((ITransactionMessage)message).DecompileInstructions().Should().ContainSingle();
        }
    }

    [TestFixture]
    public sealed class Versioned
    {
        // Same instruction as MessageV0Tests (A=[2] drains writable, B=[3] drains readonly from table [5]).
        [Test]
        public void ResolvesLookupTableAccounts()
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

            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            var decompiled = message.DecompileInstructions([table]).Should().ContainSingle().Subject;
            decompiled.ProgramId.Should().Be(Pk(9));
            decompiled.Data.Should().Equal((byte)1, 2);
            Metas(decompiled).Should().Equal(
                (Pk(2), false, true),
                (Pk(3), false, false),
                (Pk(4), false, true),
                (Pk(6), true, true));

            // Full index space = static (payer, signer, writable, program) ++ loaded-writable ++ loaded-readonly.
            message.GetAccountKeys([table]).Should().Equal(Pk(1), Pk(6), Pk(4), Pk(9), Pk(2), Pk(3));
        }

        [Test]
        public void WithoutTheTable_Throws()
        {
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Writable(Pk(2))],
                Data = [1]
            };
            var table = new AddressLookupTableAccount(Pk(5), [Pk(2)]);
            var message = MessageV0.Compile(Pk(1), Pk(8).ToString(), [instruction], [table]);

            Action act = () => _ = message.DecompileInstructions([]);
            act.Should().Throw<ArgumentException>();
        }
    }
}
