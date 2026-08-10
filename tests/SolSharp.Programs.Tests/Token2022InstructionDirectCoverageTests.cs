using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022InstructionDirectCoverageTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static byte[] Pod(byte value, int length) => [.. Enumerable.Repeat(value, length)];

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static string RepeatHex(byte value, int count)
        => string.Concat(Enumerable.Repeat(value.ToString("x2"), count));

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class UpdateConfidentialTransferMint
    {
        [Test]
        public void AuditorPodAndMultisigAuthority_MatchPinnedWireLayout()
        {
            // Act
            var instruction = Token2022Program.UpdateConfidentialTransferMint(
                Key(1), Key(2), true, Pod(3, Token2022Program.ElGamalPublicKeyLength), [Key(4), Key(5)]);

            // Assert
            Hex(instruction).Should().Be(
                "1b0101" + RepeatHex(3, Token2022Program.ElGamalPublicKeyLength));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(4), true, false),
                (Key(5), true, false));
        }

        [Test]
        public void WrongLengthAuditorPod_IsRejected()
        {
            // Act
            Action act = static () => _ = Token2022Program.UpdateConfidentialTransferMint(
                Key(1), Key(2), false, Pod(3, Token2022Program.ElGamalPublicKeyLength - 1));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("auditorElGamalPublicKey");
        }
    }

    [TestFixture]
    public sealed class EmptyConfidentialTransferAccount
    {
        [Test]
        public void InstructionOffset_UsesSignedWireByteAndInstructionsSysvarOnce()
        {
            // Act
            var instruction = Token2022Program.EmptyConfidentialTransferAccount(
                Key(1), Key(2), ConfidentialProofLocation.AtInstructionOffset(-2));

            // Assert
            Hex(instruction).Should().Be("1b04fe");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(2), true, false));
            instruction.Accounts.Count(static account => account.PublicKey == PublicKey.Parse(Sysvars.Instructions))
                .Should().Be(1);
        }

        [Test]
        public void ContextProofAndMultisigAuthority_KeepPinnedAccountOrder()
        {
            // Act
            var instruction = Token2022Program.EmptyConfidentialTransferAccount(
                Key(1), Key(2), ConfidentialProofLocation.AtContextState(Key(3)), [Key(4)]);

            // Assert
            Hex(instruction).Should().Be("1b0400");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(3), false, false),
                (Key(2), false, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class WithdrawConfidentialTokens
    {
        [Test]
        public void MixedProofLocations_MatchPinnedPodAndAccountOrder()
        {
            // Arrange
            var decryptableBalance = Pod(7, Token2022Program.DecryptableBalanceLength);

            // Act
            var instruction = Token2022Program.WithdrawConfidentialTokens(
                Key(1),
                Key(2),
                0x0102030405060708,
                9,
                decryptableBalance,
                Key(3),
                ConfidentialProofLocation.AtInstructionOffset(-3),
                ConfidentialProofLocation.AtContextState(Key(4)));

            // Assert
            Hex(instruction).Should().Be(
                "1b06080706050403020109" +
                RepeatHex(7, Token2022Program.DecryptableBalanceLength) +
                "fd00");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(4), false, false),
                (Key(3), true, false));
            instruction.Accounts.Count(static account => account.PublicKey == PublicKey.Parse(Sysvars.Instructions))
                .Should().Be(1);
        }

        [Test]
        public void WrongLengthDecryptableBalance_IsRejected()
        {
            // Act
            Action act = static () => _ = Token2022Program.WithdrawConfidentialTokens(
                Key(1),
                Key(2),
                1,
                0,
                Pod(7, Token2022Program.DecryptableBalanceLength - 1),
                Key(3),
                ConfidentialProofLocation.AtInstructionOffset(1),
                ConfidentialProofLocation.AtInstructionOffset(2));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("newDecryptableAvailableBalance");
        }
    }

    [TestFixture]
    public sealed class WithdrawConfidentialWithheldTokensFromMint
    {
        [Test]
        public void ContextProofAndMultisigAuthority_MatchPinnedWireLayout()
        {
            // Act
            var instruction = Token2022Program.WithdrawConfidentialWithheldTokensFromMint(
                Key(1),
                Key(2),
                Pod(8, Token2022Program.DecryptableBalanceLength),
                Key(3),
                ConfidentialProofLocation.AtContextState(Key(4)),
                [Key(5)]);

            // Assert
            Hex(instruction).Should().Be(
                "250100" + RepeatHex(8, Token2022Program.DecryptableBalanceLength));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(4), false, false),
                (Key(3), false, false),
                (Key(5), true, false));
        }

        [Test]
        public void WrongLengthDecryptableBalance_IsRejected()
        {
            // Act
            Action act = static () => _ = Token2022Program.WithdrawConfidentialWithheldTokensFromMint(
                Key(1),
                Key(2),
                Pod(8, Token2022Program.DecryptableBalanceLength - 1),
                Key(3),
                ConfidentialProofLocation.AtInstructionOffset(1));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("newDecryptableAvailableBalance");
        }
    }

    [TestFixture]
    public sealed class UpdateTransferHook
    {
        [Test]
        public void DirectAuthority_MatchesPinnedPointerLayout()
        {
            // Act
            var instruction = Token2022Program.UpdateTransferHook(Key(1), Key(2), Key(3));

            // Assert
            Hex(instruction).Should().Be("2401" + RepeatHex(3, PublicKey.Length));
            Metas(instruction).Should().Equal((Key(1), false, true), (Key(2), true, false));
        }

        [Test]
        public void AllZeroProgramAddress_IsRejectedAsAmbiguousNull()
        {
            // Act
            Action act = static () => _ = Token2022Program.UpdateTransferHook(Key(1), Key(2), default(PublicKey));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("transferHookProgramId");
        }
    }

    [TestFixture]
    public sealed class UpdateGroupPointer
    {
        [Test]
        public void NullAddress_MatchesPinnedZeroPodLayout()
        {
            // Act
            var instruction = Token2022Program.UpdateGroupPointer(Key(1), Key(2), null);

            // Assert
            Hex(instruction).Should().Be("2801" + new string('0', PublicKey.Length * 2));
            Metas(instruction).Should().Equal((Key(1), false, true), (Key(2), true, false));
        }
    }

    [TestFixture]
    public sealed class UpdateGroupMemberPointer
    {
        [Test]
        public void MultisigAuthority_MatchesPinnedPointerLayout()
        {
            // Act
            var instruction = Token2022Program.UpdateGroupMemberPointer(
                Key(1), Key(2), Key(3), [Key(4), Key(5)]);

            // Assert
            Hex(instruction).Should().Be("2901" + RepeatHex(3, PublicKey.Length));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(4), true, false),
                (Key(5), true, false));
        }
    }
}

public static class TransferHookProgramDirectCoverageTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class ExecuteWithExtraAccountMetas
    {
        [Test]
        public void ResolvedMetas_AreAppendedAfterValidationAccountWithoutPrivilegeChanges()
        {
            // Arrange
            AccountMeta[] additionalAccounts =
            [
                AccountMeta.Writable(Key(6)),
                AccountMeta.ReadonlySigner(Key(7))
            ];

            // Act
            var instruction = TransferHookProgram.ExecuteWithExtraAccountMetas(
                Key(9), Key(1), Key(2), Key(3), Key(4), Key(5), additionalAccounts, 100);

            // Assert
            instruction.ProgramId.Should().Be(Key(9));
            Convert.ToHexString(instruction.Data).ToLowerInvariant().Should().Be(
                "692565c54bfb661a6400000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, false),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), false, false),
                (Key(5), false, false),
                (Key(6), false, true),
                (Key(7), true, false));
        }

        [Test]
        public void NullAdditionalAccounts_IsRejected()
        {
            // Act
            Action act = static () => _ = TransferHookProgram.ExecuteWithExtraAccountMetas(
                Key(9), Key(1), Key(2), Key(3), Key(4), Key(5), null!, 1);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("additionalAccounts");
        }
    }
}
