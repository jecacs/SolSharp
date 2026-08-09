using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022TransferFeeTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class InitializeTransferFeeConfig
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.InitializeTransferFeeConfig(Key(1), Key(2), null, 250, 10_000);

            // Assert
            Hex(instruction).Should().Be(
                "1a0001" + "0202020202020202020202020202020202020202020202020202020202020202" +
                "00fa001027000000000000");
            Metas(instruction).Should().Equal((Key(1), false, true));
        }
    }

    [TestFixture]
    public sealed class TransferCheckedWithFee
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.TransferCheckedWithFee(
                Key(1), Key(2), Key(3), Key(4), 1_000, 6, 25);

            // Assert
            Hex(instruction).Should().Be("1a01e803000000000000061900000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), false, true),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class WithdrawWithheldTokensFromMint
    {
        [Test]
        public void MatchesPinnedMultisigLayout()
        {
            // Act
            var instruction = Token2022Program.WithdrawWithheldTokensFromMint(Key(1), Key(2), Key(3), [Key(4)]);

            // Assert
            Hex(instruction).Should().Be("1a02");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), false, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class WithdrawWithheldTokensFromAccounts
    {
        [Test]
        public void KeepsSignersBeforeSources()
        {
            // Act
            var instruction = Token2022Program.WithdrawWithheldTokensFromAccounts(
                Key(1),
                Key(2),
                Key(3),
                [Key(6), Key(7)],
                [Key(4), Key(5)]);

            // Assert
            Hex(instruction).Should().Be("1a0302");
            Metas(instruction).Should().Equal(
                (Key(1), false, false),
                (Key(2), false, true),
                (Key(3), false, false),
                (Key(4), true, false),
                (Key(5), true, false),
                (Key(6), false, true),
                (Key(7), false, true));
        }
    }

    [TestFixture]
    public sealed class HarvestWithheldTokensToMint
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.HarvestWithheldTokensToMint(Key(1), [Key(2), Key(3)]);

            // Assert
            Hex(instruction).Should().Be("1a04");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), false, true));
        }
    }

    [TestFixture]
    public sealed class SetTransferFee
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.SetTransferFee(Key(1), Key(2), 100, 500);

            // Assert
            Hex(instruction).Should().Be("1a056400f401000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), true, false));
        }
    }
}
