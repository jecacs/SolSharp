using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.Token2022ConfidentialTestHelpers;

namespace SolSharp.Programs.Tests;

public static class Token2022ConfidentialInstructionTests
{
    [TestFixture]
    public sealed class InitializeConfidentialTransferMint
    {
        [Test]
        public void MatchesPinnedPodLayout()
        {
            // Act
            var instruction = Token2022Program.InitializeConfidentialTransferMint(
                Key(1),
                Key(2),
                autoApproveNewAccounts: true,
                Pod(3, Token2022Program.ElGamalPublicKeyLength));

            // Assert
            Hex(instruction).Should().Be(
                "1b00" + RepeatedHex(2, PublicKey.Length) + "01" +
                RepeatedHex(3, Token2022Program.ElGamalPublicKeyLength));
        }
    }

    [TestFixture]
    public sealed class ConfigureConfidentialTransferAccount
    {
        [Test]
        public void MatchesPinnedPodLayoutAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.ConfigureConfidentialTransferAccount(
                Key(4),
                Key(1),
                Pod(5, Token2022Program.DecryptableBalanceLength),
                100,
                Key(6),
                ConfidentialProofLocation.AtInstructionOffset(-2));

            // Assert
            Hex(instruction).Should().Be(
                "1b02" + RepeatedHex(5, Token2022Program.DecryptableBalanceLength) +
                "6400000000000000fe");
            Metas(instruction).Should().Equal(
                (Key(4), false, true),
                (Key(1), false, false),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(6), true, false));
        }
    }

    [TestFixture]
    public sealed class TransferConfidentialTokens
    {
        [Test]
        public void UsesOneSysvarThenContextAccountsInProofOrder()
        {
            // Act
            var instruction = Token2022Program.TransferConfidentialTokens(
                Key(1),
                Key(2),
                Key(3),
                Pod(0xaa, Token2022Program.DecryptableBalanceLength),
                Pod(0xbb, Token2022Program.ElGamalCiphertextLength),
                Pod(0xcc, Token2022Program.ElGamalCiphertextLength),
                Key(4),
                ConfidentialProofLocation.AtInstructionOffset(1),
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtInstructionOffset(-2));

            // Assert
            instruction.Data.Should().HaveCount(169);
            Hex(instruction).Should().Be(
                "1b07" +
                string.Concat(Enumerable.Repeat("aa", Token2022Program.DecryptableBalanceLength)) +
                string.Concat(Enumerable.Repeat("bb", Token2022Program.ElGamalCiphertextLength)) +
                string.Concat(Enumerable.Repeat("cc", Token2022Program.ElGamalCiphertextLength)) +
                "0100fe");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(5), false, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class TransferConfidentialTokensWithFee
    {
        [Test]
        public void UsesPinnedInnerTagAndProofOffsets()
        {
            // Act
            var instruction = Token2022Program.TransferConfidentialTokensWithFee(
                Key(1),
                Key(2),
                Key(3),
                Pod(4, Token2022Program.DecryptableBalanceLength),
                Pod(5, Token2022Program.ElGamalCiphertextLength),
                Pod(6, Token2022Program.ElGamalCiphertextLength),
                Key(7),
                ConfidentialProofLocation.AtInstructionOffset(1),
                ConfidentialProofLocation.AtInstructionOffset(2),
                ConfidentialProofLocation.AtInstructionOffset(3),
                ConfidentialProofLocation.AtInstructionOffset(4),
                ConfidentialProofLocation.AtInstructionOffset(5));

            // Assert
            instruction.Data.Should().HaveCount(171);
            instruction.Data.Take(2).Should().Equal(27, 13);
            instruction.Data.TakeLast(5).Should().Equal(1, 2, 3, 4, 5);
        }
    }

    [TestFixture]
    public sealed class ConfigureConfidentialTransferAccountWithRegistry
    {
        [Test]
        public void UsesPinnedInnerTagAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.ConfigureConfidentialTransferAccountWithRegistry(
                Key(1), Key(2), Key(3), Key(4));

            // Assert
            instruction.Data.Should().Equal(27, 14);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), true, true),
                (PublicKey.Parse(SolanaProgramIds.SystemProgram), false, false));
        }
    }

    [TestFixture]
    public sealed class ApproveConfidentialTransferAccount
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.ApproveConfidentialTransferAccount(Key(1), Key(2), Key(3)).Data
                .Should().Equal(27, 3);
    }

    [TestFixture]
    public sealed class DepositConfidentialTokens
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.DepositConfidentialTokens(Key(1), Key(2), 3, 4, Key(5));

            // Assert
            Hex(instruction).Should().Be("1b05030000000000000004");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(5), true, false));
        }
    }

    [TestFixture]
    public sealed class ApplyConfidentialPendingBalance
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.ApplyConfidentialPendingBalance(
                Key(1), 2, Pod(3, Token2022Program.DecryptableBalanceLength), Key(4));

            // Assert
            Hex(instruction).Should().Be(
                "1b080200000000000000" + RepeatedHex(3, Token2022Program.DecryptableBalanceLength));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class EnableConfidentialCredits
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.EnableConfidentialCredits(Key(1), Key(2)).Data.Should().Equal(27, 9);
    }

    [TestFixture]
    public sealed class DisableConfidentialCredits
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.DisableConfidentialCredits(Key(1), Key(2)).Data.Should().Equal(27, 10);
    }

    [TestFixture]
    public sealed class EnableNonConfidentialCredits
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.EnableNonConfidentialCredits(Key(1), Key(2)).Data.Should().Equal(27, 11);
    }

    [TestFixture]
    public sealed class DisableNonConfidentialCredits
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.DisableNonConfidentialCredits(Key(1), Key(2)).Data.Should().Equal(27, 12);
    }

    [TestFixture]
    public sealed class InitializeConfidentialTransferFeeConfig
    {
        [Test]
        public void MatchesPinnedInterfaceData()
        {
            // Act
            var instruction = Token2022Program.InitializeConfidentialTransferFeeConfig(
                Key(1),
                null,
                Pod(2, Token2022Program.ElGamalPublicKeyLength));

            // Assert
            Hex(instruction).Should().Be(
                "2500" + new string('0', PublicKey.Length * 2) +
                RepeatedHex(2, Token2022Program.ElGamalPublicKeyLength));
        }
    }

    [TestFixture]
    public sealed class WithdrawConfidentialWithheldTokensFromAccounts
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.WithdrawConfidentialWithheldTokensFromAccounts(
                Key(1),
                Key(2),
                Pod(3, Token2022Program.DecryptableBalanceLength),
                Key(4),
                [Key(7), Key(8)],
                ConfidentialProofLocation.AtContextState(Key(5)),
                [Key(6)]);

            // Assert
            Hex(instruction).Should().Be(
                "25020200" + RepeatedHex(3, Token2022Program.DecryptableBalanceLength));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(5), false, false),
                (Key(4), false, false),
                (Key(6), true, false),
                (Key(7), false, true),
                (Key(8), false, true));
        }
    }

    [TestFixture]
    public sealed class HarvestConfidentialWithheldTokensToMint
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.HarvestConfidentialWithheldTokensToMint(Key(1), [Key(2)]).Data
                .Should().Equal(37, 3);
    }

    [TestFixture]
    public sealed class EnableConfidentialHarvestToMint
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.EnableConfidentialHarvestToMint(Key(1), Key(2)).Data.Should().Equal(37, 4);
    }

    [TestFixture]
    public sealed class DisableConfidentialHarvestToMint
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.DisableConfidentialHarvestToMint(Key(1), Key(2)).Data.Should().Equal(37, 5);
    }

    [TestFixture]
    public sealed class InitializeConfidentialMintBurn
    {
        [Test]
        public void MatchesPinnedInterfaceData()
        {
            // Act
            var instruction = Token2022Program.InitializeConfidentialMintBurn(
                Key(1),
                Pod(2, Token2022Program.ElGamalPublicKeyLength),
                Pod(3, Token2022Program.DecryptableBalanceLength));

            // Assert
            Hex(instruction).Should().Be(
                "2a00" + RepeatedHex(2, Token2022Program.ElGamalPublicKeyLength) +
                RepeatedHex(3, Token2022Program.DecryptableBalanceLength));
        }
    }

    [TestFixture]
    public sealed class MintConfidentialTokens
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndProofOrdering()
        {
            // Act
            var instruction = Token2022Program.MintConfidentialTokens(
                Key(1),
                Key(2),
                Pod(3, Token2022Program.DecryptableBalanceLength),
                Pod(4, Token2022Program.ElGamalCiphertextLength),
                Pod(5, Token2022Program.ElGamalCiphertextLength),
                Key(6),
                ConfidentialProofLocation.AtContextState(Key(7)),
                ConfidentialProofLocation.AtInstructionOffset(1),
                ConfidentialProofLocation.AtContextState(Key(8)));

            // Assert
            instruction.Data.Should().HaveCount(169);
            instruction.Data.Take(2).Should().Equal(42, 3);
            instruction.Data.TakeLast(3).Should().Equal(0, 1, 0);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(7), false, false),
                (Key(8), false, false),
                (Key(6), true, false));
        }
    }

    [TestFixture]
    public sealed class RotateConfidentialSupplyElGamalPublicKey
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.RotateConfidentialSupplyElGamalPublicKey(
                Key(1),
                Key(2),
                Pod(3, Token2022Program.ElGamalPublicKeyLength),
                ConfidentialProofLocation.AtInstructionOffset(1));

            // Assert
            Hex(instruction).Should().Be(
                "2a01" + RepeatedHex(3, Token2022Program.ElGamalPublicKeyLength) + "01");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(2), true, false));
        }
    }

    [TestFixture]
    public sealed class UpdateConfidentialDecryptableSupply
    {
        [Test]
        public void MatchesPinnedInterfaceDataAndAccountOrdering()
        {
            // Act
            var instruction = Token2022Program.UpdateConfidentialDecryptableSupply(
                Key(1), Key(2), Pod(3, Token2022Program.DecryptableBalanceLength));

            // Assert
            Hex(instruction).Should().Be(
                "2a02" + RepeatedHex(3, Token2022Program.DecryptableBalanceLength));
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), true, false));
        }
    }

    [TestFixture]
    public sealed class BurnConfidentialTokens
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.BurnConfidentialTokens(
                    Key(1),
                    Key(2),
                    Pod(3, 36),
                    Pod(4, 64),
                    Pod(5, 64),
                    Key(6),
                    ConfidentialProofLocation.AtInstructionOffset(1),
                    ConfidentialProofLocation.AtInstructionOffset(2),
                    ConfidentialProofLocation.AtInstructionOffset(3)).Data.Take(2)
                .Should().Equal(42, 4);
    }

    [TestFixture]
    public sealed class ApplyPendingConfidentialBurn
    {
        [Test]
        public void UsesPinnedInnerTag()
            => Token2022Program.ApplyPendingConfidentialBurn(Key(1), Key(2)).Data.Should().Equal(42, 5);
    }
}

public static class ElGamalProofProgramTests
{
    [TestFixture]
    public sealed class VerifyProof
    {
        [Test]
        public void MatchesPinnedNativeInterface()
        {
            // Act
            var instruction = ElGamalProofProgram.VerifyProof(
                ElGamalProofInstruction.VerifyPubkeyValidity,
                Pod(3, 96),
                Key(4),
                Key(5));

            // Assert
            instruction.Data.Should().HaveCount(97);
            instruction.Data[0].Should().Be(4);
            instruction.Data.AsSpan(1).ToArray().Should().OnlyContain(static value => value == 3);
            Metas(instruction).Should().Equal((Key(4), false, true), (Key(5), false, false));
        }
    }

    [TestFixture]
    public sealed class VerifyProofFromAccount
    {
        [Test]
        public void MatchesPinnedNativeInterface()
        {
            // Act
            var instruction = ElGamalProofProgram.VerifyProofFromAccount(
                ElGamalProofInstruction.VerifyBatchedRangeProofU128,
                Key(6),
                0x11223344);

            // Assert
            instruction.Data.Should().Equal(7, 0x44, 0x33, 0x22, 0x11);
            Metas(instruction).Should().Equal((Key(6), false, false));
        }
    }

    [TestFixture]
    public sealed class CloseContextState
    {
        [Test]
        public void MatchesPinnedNativeInterface()
        {
            // Act
            var instruction = ElGamalProofProgram.CloseContextState(Key(1), Key(2), Key(3));

            // Assert
            instruction.Data.Should().Equal(0);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), true, false));
        }
    }
}

public static class ElGamalRegistryProgramTests
{
    [TestFixture]
    public sealed class CreateRegistry
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = ElGamalRegistryProgram.CreateRegistry(
                Key(1), ConfidentialProofLocation.AtInstructionOffset(1));

            // Assert
            instruction.Data.Should().Equal(0, 1);
            Metas(instruction).Should().Equal(
                (ElGamalRegistryProgram.GetRegistryAddress(Key(1)), false, true),
                (Key(1), true, false),
                (PublicKey.Parse(SolanaProgramIds.SystemProgram), false, false),
                (PublicKey.Parse(Sysvars.Instructions), false, false));
        }
    }

    [TestFixture]
    public sealed class UpdateRegistry
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = ElGamalRegistryProgram.UpdateRegistry(
                Key(1), ConfidentialProofLocation.AtContextState(Key(2)));

            // Assert
            instruction.Data.Should().Equal(1, 0);
            Metas(instruction).Should().Equal(
                (ElGamalRegistryProgram.GetRegistryAddress(Key(1)), false, true),
                (Key(2), false, false),
                (Key(1), true, false));
        }
    }

    [TestFixture]
    public sealed class DecodeState
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Arrange
            var data = Key(3).ToBytes().Concat(Pod(4, 32)).ToArray();

            // Act
            var state = ElGamalRegistryProgram.DecodeState(data);

            // Assert
            state.Should().NotBeNull();
            state.Owner.Should().Be(Key(3));
            state.ElGamalPublicKey.ToArray().Should().Equal(Pod(4, 32));
        }
    }
}

internal static class Token2022ConfidentialTestHelpers
{
    internal static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    internal static byte[] Pod(byte value, int length) => [.. Enumerable.Repeat(value, length)];

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    internal static string RepeatedHex(byte value, int length)
        => string.Concat(Enumerable.Repeat(value.ToString("x2"), length));

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];
}
