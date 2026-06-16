using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class TokenProgramTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class Transfer
    {
        // Reference from solana-py: transfer(source=[4], dest=[5], owner=[6], amount=1000).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = TokenProgram.Transfer(Key(4), Key(5), Key(6), 1000);

            instruction.ProgramId.Should().Be(TokenProgram.ProgramId);
            instruction.Data.Should().Equal(Hex("03e803000000000000"));
            instruction.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable)).Should().Equal(
                (Key(4), false, true),
                (Key(5), false, true),
                (Key(6), true, false));
        }
    }

    [TestFixture]
    public sealed class TransferChecked
    {
        // Reference from solana-py: transfer_checked(source=[4], mint=[3], dest=[5], owner=[6], amount=1000, decimals=6).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = TokenProgram.TransferChecked(Key(4), Key(3), Key(5), Key(6), 1000, 6);

            instruction.ProgramId.Should().Be(TokenProgram.ProgramId);
            instruction.Data.Should().Equal(Hex("0ce80300000000000006"));
            instruction.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable)).Should().Equal(
                (Key(4), false, true),
                (Key(3), false, false),
                (Key(5), false, true),
                (Key(6), true, false));
        }
    }
}
