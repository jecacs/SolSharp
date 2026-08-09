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
            // Arrange
            var from = Key(1);
            var to = Key(2);

            // Act
            var instruction = SystemProgram.Transfer(from, to, 1_000_000);

            // Assert
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
    public sealed class TransferMany
    {
        [Test]
        public void PreservesOrderAndUsesCanonicalTransferWire()
        {
            // Act
            var instructions = SystemProgram.TransferMany(Key(1), (Key(2), 7), (Key(3), 9));

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Data.Should().Equal(Hex("020000000700000000000000"));
            instructions[1].Data.Should().Equal(Hex("020000000900000000000000"));
            Metas(instructions[0]).Should().Equal((Key(1), true, true), (Key(2), false, true));
            Metas(instructions[1]).Should().Equal((Key(1), true, true), (Key(3), false, true));
        }

        [Test]
        public void EmptyInput_ReturnsEmptyArray()
            => SystemProgram.TransferMany(Key(1)).Should().BeEmpty();

        [Test]
        public void NullInput_Throws()
        {
            // Act
            Action act = () => _ = SystemProgram.TransferMany(Key(1), null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("transfers");
        }
    }

    [TestFixture]
    public sealed class CreateAccount
    {
        // Reference bytes from solders: create_account(from=[1;32], new=[2;32], lamports=2_039_280, space=165, owner=[9;32]).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Arrange
            var from = Key(1);
            var newAccount = Key(2);
            var owner = Key(9);

            // Act
            var instruction = SystemProgram.CreateAccount(from, newAccount, 2_039_280, 165, owner);

            // Assert
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

    [TestFixture]
    public sealed class CreateAccountAllowPrefund
    {
        // Exact layout from the pinned generated System client: discriminator 13 followed by
        // lamports, space, and owner. The payer account is optional and follows the new account.
        [Test]
        public void MatchesGeneratedSystemClientWithoutPayer()
        {
            // Act
            var instruction = SystemProgram.CreateAccountAllowPrefund(Key(2), 165, Key(9));

            // Assert
            instruction.Data.Should().Equal(Hex(
                "0d0000000000000000000000a500000000000000" +
                "0909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), true, true));
        }

        [Test]
        public void MatchesGeneratedSystemClientWithPayer()
        {
            // Act
            var instruction = SystemProgram.CreateAccountAllowPrefund(Key(2), 165, Key(9), 42, Key(1));

            // Assert
            instruction.Data.Should().Equal(Hex(
                "0d0000002a00000000000000a500000000000000" +
                "0909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), true, true), (Key(1), true, true));
        }

        [Test]
        public void AdditionalLamportsWithoutPayer_ThrowsArgumentException()
        {
            // Act
            Action act = () => _ = SystemProgram.CreateAccountAllowPrefund(Key(2), 165, Key(9), lamports: 1);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("payer");
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
            // Act
            var instruction = SystemProgram.Assign(Key(2), Key(9));

            // Assert
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
            // Act
            var instruction = SystemProgram.Allocate(Key(2), 200);

            // Assert
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
            // Act
            var instruction = SystemProgram.CreateAccountWithSeed(Key(1), Key(8), Key(2), "hello", 42, 100, Key(9));

            // Assert
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
            // Act
            var instruction = SystemProgram.InitializeNonceAccount(Key(2), Key(3));

            // Assert
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
            // Act
            var instruction = SystemProgram.AdvanceNonceAccount(Key(2), Key(3));

            // Assert
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
            // Act
            var instruction = SystemProgram.WithdrawNonceAccount(Key(2), Key(3), Key(5), 1000);

            // Assert
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
            // Act
            var instruction = SystemProgram.AuthorizeNonceAccount(Key(2), Key(3), Key(7));

            // Assert
            instruction.Data.Should().Equal(Hex("070000000707070707070707070707070707070707070707070707070707070707070707"));
            Metas(instruction).Should().Equal((Key(2), false, true), (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class UpgradeNonceAccount
    {
        // Reference from the pinned generated Rust client: discriminator 12 and one writable non-signer.
        [Test]
        public void MatchesGeneratedSolanaClient()
        {
            // Act
            var instruction = SystemProgram.UpgradeNonceAccount(Key(2));

            // Assert
            instruction.ProgramId.Should().Be(PublicKey.Parse(SolanaProgramIds.SystemProgram));
            instruction.Data.Should().Equal(Hex("0c000000"));
            Metas(instruction).Should().Equal((Key(2), false, true));
        }
    }

    [TestFixture]
    public sealed class AllocateWithSeed
    {
        // Reference bytes from solders: allocate_with_seed(address=[2], base=[3], seed="hello", space=165, owner=[9]).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instruction = SystemProgram.AllocateWithSeed(Key(2), Key(3), "hello", 165, Key(9));

            // Assert
            instruction.Data.Should().Equal(Hex(
                "090000000303030303030303030303030303030303030303030303030303030303030303050000000000000068656c6c6fa5000000000000000909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), false, true), (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class AssignWithSeed
    {
        // Reference bytes from solders: assign_with_seed(address=[2], base=[3], seed="hello", owner=[9]).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instruction = SystemProgram.AssignWithSeed(Key(2), Key(3), "hello", Key(9));

            // Assert
            instruction.Data.Should().Equal(Hex(
                "0a0000000303030303030303030303030303030303030303030303030303030303030303050000000000000068656c6c6f0909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), false, true), (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class TransferWithSeed
    {
        // Reference bytes from solders: transfer_with_seed(from=[2], base=[3], seed="hello", owner=[9], to=[4], lamports=777).
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instruction = SystemProgram.TransferWithSeed(Key(2), Key(3), "hello", Key(9), Key(4), 777);

            // Assert
            instruction.Data.Should().Equal(Hex(
                "0b0000000903000000000000050000000000000068656c6c6f0909090909090909090909090909090909090909090909090909090909090909"));
            Metas(instruction).Should().Equal((Key(2), false, true), (Key(3), true, false), (Key(4), false, true));
        }
    }

    [TestFixture]
    public sealed class CreateNonceAccount
    {
        // Reference bytes from solders: create_nonce_account(from=[1], nonce=[2], authority=[3], lamports=1_447_680)
        // - a CreateAccount of NONCE_ACCOUNT_LENGTH (80) bytes owned by the System program, then InitializeNonceAccount.
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var instructions = SystemProgram.CreateNonceAccount(Key(1), Key(2), Key(3), 1_447_680);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Data.Should().Equal(Hex(
                "0000000000171600000000005000000000000000" +
                "0000000000000000000000000000000000000000000000000000000000000000"));
            Metas(instructions[0]).Should().Equal((Key(1), true, true), (Key(2), true, true));

            instructions[1].Data.Should().Equal(Hex("060000000303030303030303030303030303030303030303030303030303030303030303"));
            Metas(instructions[1]).Should().Equal((Key(2), false, true), (RecentBlockhashes, false, false), (Rent, false, false));
        }
    }

    [TestFixture]
    public sealed class CreateNonceAccountWithSeed
    {
        // Exact bincode layout from pinned solana-system-interface create_nonce_account_with_seed.
        [Test]
        public void MatchesPinnedSystemInterface()
        {
            // Act
            var instructions = SystemProgram.CreateNonceAccountWithSeed(
                Key(1), Key(8), Key(2), "hello", Key(3), 1_447_680);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Data.Should().Equal(Hex(
                "03000000" +
                "0202020202020202020202020202020202020202020202020202020202020202" +
                "050000000000000068656c6c6f00171600000000005000000000000000" +
                "0000000000000000000000000000000000000000000000000000000000000000"));
            Metas(instructions[0]).Should().Equal(
                (Key(1), true, true),
                (Key(8), false, true),
                (Key(2), true, false));
            instructions[1].Data.Should().Equal(Hex(
                "060000000303030303030303030303030303030303030303030303030303030303030303"));
            Metas(instructions[1]).Should().Equal(
                (Key(8), false, true),
                (RecentBlockhashes, false, false),
                (Rent, false, false));
        }
    }

    [TestFixture]
    public sealed class WithSeedValidation
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void SeedLongerThanThirtyTwoUtf8Bytes_Throws(int operation)
        {
            // Arrange
            var seed = new string('a', 33);
            Action act = operation switch
            {
                0 => () => _ = SystemProgram.CreateAccountWithSeed(Key(1), Key(2), Key(3), seed, 1, 1, Key(9)),
                1 => () => _ = SystemProgram.AllocateWithSeed(Key(2), Key(3), seed, 1, Key(9)),
                2 => () => _ = SystemProgram.AssignWithSeed(Key(2), Key(3), seed, Key(9)),
                _ => () => _ = SystemProgram.TransferWithSeed(Key(2), Key(3), seed, Key(9), Key(4), 1)
            };

            // Act & Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 32 bytes*33*");
        }

        [Test]
        public void ThirtyTwoUtf8Bytes_IsAccepted()
        {
            // Act
            Action act = () => _ = SystemProgram.CreateAccountWithSeed(
                Key(1), Key(2), Key(3), new string('a', 32), 1, 1, Key(9));

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void LimitIsMeasuredInUtf8Bytes_NotCharacters()
        {
            // Arrange: seventeen two-byte characters occupy 34 bytes.
            var seed = new string('\u00E9', 17);

            // Act
            Action act = () => _ = SystemProgram.AssignWithSeed(Key(2), Key(3), seed, Key(9));

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*34*");
        }

        [Test]
        public void UnpairedSurrogate_ThrowsInsteadOfEncodingReplacementCharacter()
        {
            // Arrange
            const string seed = "\uD800";

            // Act
            Action act = () => _ = SystemProgram.AssignWithSeed(Key(2), Key(3), seed, Key(9));

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName(nameof(seed))
                .WithMessage("*valid Unicode*");
        }
    }
}
