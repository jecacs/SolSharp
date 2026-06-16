using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class SystemProgramTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class Transfer
    {
        // Reference bytes from solders: transfer(from=[1;32], to=[2;32], lamports=1_000_000).
        [Test]
        public void MatchesSolanaSdk()
        {
            var from = Key(1);
            var to = Key(2);

            var instruction = SystemProgram.Transfer(from, to, 1_000_000);

            instruction.ProgramId.Should().Be(PublicKey.Parse(SolanaProgramIds.SystemProgram));
            instruction.Data.Should().Equal(Hex("0200000040420f0000000000"));
            instruction.Accounts.Should().HaveCount(2);
            instruction.Accounts[0].PublicKey.Should().Be(from);
            instruction.Accounts[0].IsSigner.Should().BeTrue();
            instruction.Accounts[0].IsWritable.Should().BeTrue();
            instruction.Accounts[1].PublicKey.Should().Be(to);
            instruction.Accounts[1].IsSigner.Should().BeFalse();
            instruction.Accounts[1].IsWritable.Should().BeTrue();
        }
    }

    [TestFixture]
    public sealed class CreateAccount
    {
        // Reference bytes from solders: create_account(from=[1;32], new=[2;32], lamports=2_039_280, space=165, owner=[9;32]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var from = Key(1);
            var newAccount = Key(2);
            var owner = Key(9);

            var instruction = SystemProgram.CreateAccount(from, newAccount, 2_039_280, 165, owner);

            instruction.Data.Should().Equal(Hex(
                "00000000f01d1f0000000000a500000000000000" +
                "0909090909090909090909090909090909090909090909090909090909090909"));
            instruction.Accounts.Should().HaveCount(2);
            instruction.Accounts[0].PublicKey.Should().Be(from);
            instruction.Accounts[0].IsSigner.Should().BeTrue();
            instruction.Accounts[0].IsWritable.Should().BeTrue();
            instruction.Accounts[1].PublicKey.Should().Be(newAccount);
            instruction.Accounts[1].IsSigner.Should().BeTrue();
            instruction.Accounts[1].IsWritable.Should().BeTrue();
        }
    }
}
