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

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable))];

    private static PublicKey RecentBlockhashes => PublicKey.Parse(Sysvars.RecentBlockhashes);
    private static PublicKey Rent => PublicKey.Parse(Sysvars.Rent);

    [TestFixture]
    public sealed class Assign
    {
        // Reference bytes from solders: assign(pubkey=[2;32], owner=[9;32]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.Assign(Key(2), Key(9));

            instruction.Data.Should().Equal(Hex("010000000909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), true, true));
        }
    }

    [TestFixture]
    public sealed class Allocate
    {
        // Reference bytes from solders: allocate(pubkey=[2;32], space=200).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.Allocate(Key(2), 200);

            instruction.Data.Should().Equal(Hex("08000000c800000000000000"));
            Metas(instruction).Should().Equal((Key(2), true, true));
        }
    }

    [TestFixture]
    public sealed class CreateAccountWithSeed
    {
        // Reference bytes from solders: create_account_with_seed(from=[1], base=[2], seed="hello", lamports=42, space=100, owner=[9]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.CreateAccountWithSeed(Key(1), Key(8), Key(2), "hello", 42, 100, Key(9));

            instruction.Data.Should().Equal(Hex(
                "030000000202020202020202020202020202020202020202020202020202020202020202050000000000000068656c6c6f2a0000000000000064000000000000000909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(1), true, true), (Key(8), false, true), (Key(2), true, false));
        }
    }

    [TestFixture]
    public sealed class InitializeNonceAccount
    {
        // Reference bytes from solders: initialize_nonce_account(nonce=[2], authority=[3]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.InitializeNonceAccount(Key(2), Key(3));

            instruction.Data.Should().Equal(Hex("060000000303030303030303030303030303030303030303030303030303030303030303"));
            Metas(instruction).Should().Equal((Key(2), false, true), (RecentBlockhashes, false, false), (Rent, false, false));
        }
    }

    [TestFixture]
    public sealed class AdvanceNonceAccount
    {
        // Reference bytes from solders: advance_nonce_account(nonce=[2], authority=[3]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.AdvanceNonceAccount(Key(2), Key(3));

            instruction.Data.Should().Equal(Hex("04000000"));
            Metas(instruction).Should().Equal((Key(2), false, true), (RecentBlockhashes, false, false), (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class WithdrawNonceAccount
    {
        // Reference bytes from solders: withdraw_nonce_account(nonce=[2], authority=[3], to=[5], lamports=1000).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.WithdrawNonceAccount(Key(2), Key(3), Key(5), 1000);

            instruction.Data.Should().Equal(Hex("05000000e803000000000000"));
            Metas(instruction).Should().Equal(
                (Key(2), false, true),
                (Key(5), false, true),
                (RecentBlockhashes, false, false),
                (Rent, false, false),
                (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class AuthorizeNonceAccount
    {
        // Reference bytes from solders: authorize_nonce_account(nonce=[2], authority=[3], new_authority=[7]).
        [Test]
        public void MatchesSolanaSdk()
        {
            var instruction = SystemProgram.AuthorizeNonceAccount(Key(2), Key(3), Key(7));

            instruction.Data.Should().Equal(Hex("070000000707070707070707070707070707070707070707070707070707070707070707"));
            Metas(instruction).Should().Equal((Key(2), false, true), (Key(3), true, false));
        }
    }
}
