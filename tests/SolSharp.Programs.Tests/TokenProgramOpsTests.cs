using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class TokenProgramOpsTests
{
    private const string RentSysvar = "SysvarRent111111111111111111111111111111111";

    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    private static string DataHex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static void Check(AccountMeta meta, PublicKey key, bool signer, bool writable)
    {
        meta.PublicKey.Should().Be(key);
        meta.IsSigner.Should().Be(signer);
        meta.IsWritable.Should().Be(writable);
    }

    // Reference data/accounts from solana-py spl.token.instructions; amounts are 1000.
    [TestFixture]
    public sealed class AmountOps
    {
        [Test]
        public void MintTo_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.MintTo(Pk(3), Pk(5), Pk(6), 1000);

            // Assert
            DataHex(ix).Should().Be("07e803000000000000");
            ix.Accounts.Should().HaveCount(3);
            Check(ix.Accounts[0], Pk(3), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(5), signer: false, writable: true);
            Check(ix.Accounts[2], Pk(6), signer: true, writable: false);
        }

        [Test]
        public void Burn_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.Burn(Pk(2), Pk(3), Pk(6), 1000);

            // Assert
            DataHex(ix).Should().Be("08e803000000000000");
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: true);
            Check(ix.Accounts[2], Pk(6), signer: true, writable: false);
        }

        [Test]
        public void Approve_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.Approve(Pk(2), Pk(4), Pk(6), 1000);

            // Assert
            DataHex(ix).Should().Be("04e803000000000000");
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(4), signer: false, writable: false);
            Check(ix.Accounts[2], Pk(6), signer: true, writable: false);
        }
    }

    // Reference data/accounts from solana-py spl.token.instructions; amounts are 1000, decimals 6.
    [TestFixture]
    public sealed class CheckedOps
    {
        [Test]
        public void ApproveChecked_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.ApproveChecked(Pk(2), Pk(3), Pk(4), Pk(5), 1000, 6);

            // Assert
            DataHex(ix).Should().Be("0de80300000000000006");
            ix.Accounts.Should().HaveCount(4);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: false);
            Check(ix.Accounts[2], Pk(4), signer: false, writable: false);
            Check(ix.Accounts[3], Pk(5), signer: true, writable: false);
        }

        [Test]
        public void MintToChecked_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.MintToChecked(Pk(2), Pk(3), Pk(4), 1000, 6);

            // Assert
            DataHex(ix).Should().Be("0ee80300000000000006");
            ix.Accounts.Should().HaveCount(3);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: true);
            Check(ix.Accounts[2], Pk(4), signer: true, writable: false);
        }

        [Test]
        public void BurnChecked_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.BurnChecked(Pk(2), Pk(3), Pk(4), 1000, 6);

            // Assert
            DataHex(ix).Should().Be("0fe80300000000000006");
            ix.Accounts.Should().HaveCount(3);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: true);
            Check(ix.Accounts[2], Pk(4), signer: true, writable: false);
        }
    }

    [TestFixture]
    public sealed class SetAuthority
    {
        // Reference from solana-py: set_authority(account=[2], authority=ACCOUNT_OWNER, current=[3], new=[4]).
        [Test]
        public void WithNewAuthority_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.SetAuthority(Pk(2), Pk(3), AuthorityType.AccountOwner, Pk(4));

            // Assert
            DataHex(ix).Should().Be("0602010404040404040404040404040404040404040404040404040404040404040404");
            ix.Accounts.Should().HaveCount(2);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: true, writable: false);
        }

        // Removing an authority packs the compact Rust spl-token COption: a lone 0 tag with no value.
        // (solana-py pads None with 32 zero bytes; the program unpacks both forms.)
        [Test]
        public void WithoutNewAuthority_PacksCompactNone()
        {
            // Act
            var ix = TokenProgram.SetAuthority(Pk(2), Pk(3), AuthorityType.CloseAccount);

            // Assert
            DataHex(ix).Should().Be("060300");
            ix.Accounts.Should().HaveCount(2);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: true, writable: false);
        }

        [Test]
        public void WithToken2022AuthorityType_EncodesItsWireValue()
        {
            // Act: change a Token-2022 mint's transfer-fee authority.
            var token2022 = PublicKey.Parse(SolanaProgramIds.Token2022Program);
            var ix = TokenProgram.SetAuthority(Pk(2), Pk(3), AuthorityType.TransferFeeConfig, Pk(4), token2022);

            // Assert
            ix.ProgramId.Should().Be(token2022);
            DataHex(ix).Should().Be("0604010404040404040404040404040404040404040404040404040404040404040404");
        }

        [Test]
        public void UndefinedAuthorityType_Throws()
        {
            // Act
            Action act = () => _ = TokenProgram.SetAuthority(Pk(2), Pk(3), (AuthorityType)byte.MaxValue);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("authorityType");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Token2022AuthorityWithClassicProgram_Throws(bool explicitProgram)
        {
            // Arrange
            PublicKey? tokenProgram = explicitProgram ? TokenProgram.ProgramId : null;

            // Act
            Action act = () => _ = TokenProgram.SetAuthority(
                Pk(2), Pk(3), AuthorityType.TransferFeeConfig, Pk(4), tokenProgram);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("authorityType");
        }

        [Test]
        public void Token2022AuthorityWithCustomProgram_IsCallerOwned()
        {
            // Arrange
            var customProgram = Pk(9);

            // Act
            var instruction = TokenProgram.SetAuthority(
                Pk(2), Pk(3), AuthorityType.TransferFeeConfig, Pk(4), customProgram);

            // Assert
            instruction.ProgramId.Should().Be(customProgram);
            instruction.Data[1].Should().Be((byte)AuthorityType.TransferFeeConfig);
        }

        // The numbering mirrors spl-token-2022's AuthorityType (interface/src/instruction.rs); a wrong
        // value here is a wire bug, so every variant is pinned.
        [TestCase(AuthorityType.MintTokens, 0)]
        [TestCase(AuthorityType.FreezeAccount, 1)]
        [TestCase(AuthorityType.AccountOwner, 2)]
        [TestCase(AuthorityType.CloseAccount, 3)]
        [TestCase(AuthorityType.TransferFeeConfig, 4)]
        [TestCase(AuthorityType.WithheldWithdraw, 5)]
        [TestCase(AuthorityType.CloseMint, 6)]
        [TestCase(AuthorityType.InterestRate, 7)]
        [TestCase(AuthorityType.PermanentDelegate, 8)]
        [TestCase(AuthorityType.ConfidentialTransferMint, 9)]
        [TestCase(AuthorityType.TransferHookProgramId, 10)]
        [TestCase(AuthorityType.ConfidentialTransferFeeConfig, 11)]
        [TestCase(AuthorityType.MetadataPointer, 12)]
        [TestCase(AuthorityType.GroupPointer, 13)]
        [TestCase(AuthorityType.GroupMemberPointer, 14)]
        [TestCase(AuthorityType.ScaledUiAmount, 15)]
        [TestCase(AuthorityType.Pause, 16)]
        [TestCase(AuthorityType.PermissionedBurn, 17)]
        public void AuthorityType_MatchesSplTokenNumbering(AuthorityType type, int expected)
            => ((byte)type).Should().Be((byte)expected);
    }

    [TestFixture]
    public sealed class SimpleOps
    {
        [Test]
        public void Revoke_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.Revoke(Pk(2), Pk(6));

            // Assert
            DataHex(ix).Should().Be("05");
            ix.Accounts.Should().HaveCount(2);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(6), signer: true, writable: false);
        }

        [Test]
        public void CloseAccount_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.CloseAccount(Pk(2), Pk(5), Pk(6));

            // Assert
            DataHex(ix).Should().Be("09");
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(5), signer: false, writable: true);
            Check(ix.Accounts[2], Pk(6), signer: true, writable: false);
        }

        [Test]
        public void SyncNative_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.SyncNative(Pk(2));

            // Assert
            DataHex(ix).Should().Be("11");
            ix.Accounts.Should().ContainSingle();
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
        }

        [Test]
        public void FreezeAndThaw_MatchSolanaPy()
        {
            // Act & Assert: freeze
            var freeze = TokenProgram.FreezeAccount(Pk(2), Pk(3), Pk(6));
            DataHex(freeze).Should().Be("0a");
            Check(freeze.Accounts[0], Pk(2), signer: false, writable: true);
            Check(freeze.Accounts[1], Pk(3), signer: false, writable: false);
            Check(freeze.Accounts[2], Pk(6), signer: true, writable: false);

            // Act & Assert: thaw
            var thaw = TokenProgram.ThawAccount(Pk(2), Pk(3), Pk(6));
            DataHex(thaw).Should().Be("0b");
        }
    }

    [TestFixture]
    public sealed class Initialize
    {
        [Test]
        public void InitializeAccount_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.InitializeAccount(Pk(2), Pk(3), Pk(6));

            // Assert
            DataHex(ix).Should().Be("01");
            ix.Accounts.Should().HaveCount(4);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: false);
            Check(ix.Accounts[2], Pk(6), signer: false, writable: false);
            ix.Accounts[3].PublicKey.Should().Be(PublicKey.Parse(RentSysvar));
        }

        [Test]
        public void InitializeMint_WithFreezeAuthority_MatchesSolanaPy()
        {
            // Act
            var ix = TokenProgram.InitializeMint(Pk(3), 6, Pk(6), Pk(7));

            // Assert
            DataHex(ix).Should().Be(
                "0006" + "0606060606060606060606060606060606060606060606060606060606060606" +
                "01" + "0707070707070707070707070707070707070707070707070707070707070707");
            Check(ix.Accounts[0], Pk(3), signer: false, writable: true);
            ix.Accounts[1].PublicKey.Should().Be(PublicKey.Parse(RentSysvar));
        }

        [Test]
        public void InitializeMint_NoFreezeAuthority_UsesMinimalForm()
        {
            // Minimal spl-token form: a None freeze authority is a single 0 byte (35 bytes total), not the
            // 67-byte zero-padded form some encoders emit (which the program tolerates as trailing data).
            // Act
            var ix = TokenProgram.InitializeMint(Pk(3), 6, Pk(6));

            // Assert
            DataHex(ix).Should().Be(
                "0006" + "0606060606060606060606060606060606060606060606060606060606060606" + "00");
        }
    }

    [TestFixture]
    public sealed class MultisigAuthority
    {
        private static readonly PublicKey[] Members = [Pk(7), Pk(8)];

        [Test]
        public void TransferChecked_MatchesOfficialMultisigLayout()
        {
            // Act
            var ix = TokenProgram.TransferChecked(
                Pk(2), Pk(3), Pk(4), Pk(6), 1000, 6, tokenProgram: null, multisigSigners: Members);

            // Assert
            DataHex(ix).Should().Be("0ce80300000000000006");
            ix.Accounts.Should().HaveCount(6);
            Check(ix.Accounts[0], Pk(2), signer: false, writable: true);
            Check(ix.Accounts[1], Pk(3), signer: false, writable: false);
            Check(ix.Accounts[2], Pk(4), signer: false, writable: true);
            Check(ix.Accounts[3], Pk(6), signer: false, writable: false);
            Check(ix.Accounts[4], Members[0], signer: true, writable: false);
            Check(ix.Accounts[5], Members[1], signer: true, writable: false);
        }

        [Test]
        public void SetAuthority_Token2022_MatchesOfficialMultisigLayout()
        {
            // Arrange
            var token2022 = PublicKey.Parse(SolanaProgramIds.Token2022Program);

            // Act
            var ix = TokenProgram.SetAuthority(
                Pk(2), Pk(6), AuthorityType.TransferFeeConfig, Pk(4), token2022, Members);

            // Assert
            ix.ProgramId.Should().Be(token2022);
            DataHex(ix).Should().Be("0604010404040404040404040404040404040404040404040404040404040404040404");
            CheckMultisig(ix, authorityIndex: 1, Pk(6));
        }

        [Test]
        public void EveryAuthorityOperation_UsesNonSignerAuthorityThenMemberSigners()
        {
            // Act
            (Instruction Instruction, int AuthorityIndex, PublicKey Authority)[] instructions =
            [
                (TokenProgram.Transfer(Pk(2), Pk(3), Pk(6), 1, null, Members), 2, Pk(6)),
                (TokenProgram.MintTo(Pk(2), Pk(3), Pk(6), 1, null, Members), 2, Pk(6)),
                (TokenProgram.Burn(Pk(2), Pk(3), Pk(6), 1, null, Members), 2, Pk(6)),
                (TokenProgram.Approve(Pk(2), Pk(3), Pk(6), 1, null, Members), 2, Pk(6)),
                (TokenProgram.Revoke(Pk(2), Pk(6), null, Members), 1, Pk(6)),
                (TokenProgram.CloseAccount(Pk(2), Pk(3), Pk(6), null, Members), 2, Pk(6)),
                (TokenProgram.FreezeAccount(Pk(2), Pk(3), Pk(6), null, Members), 2, Pk(6)),
                (TokenProgram.ThawAccount(Pk(2), Pk(3), Pk(6), null, Members), 2, Pk(6)),
                (TokenProgram.ApproveChecked(Pk(2), Pk(3), Pk(4), Pk(6), 1, 6, null, Members), 3, Pk(6)),
                (TokenProgram.MintToChecked(Pk(2), Pk(3), Pk(6), 1, 6, null, Members), 2, Pk(6)),
                (TokenProgram.BurnChecked(Pk(2), Pk(3), Pk(6), 1, 6, null, Members), 2, Pk(6))
            ];

            // Assert
            foreach (var (instruction, authorityIndex, authority) in instructions)
                CheckMultisig(instruction, authorityIndex, authority);
        }

        [Test]
        public void EmptyMemberList_Throws()
        {
            // Act
            Action act = () => _ = TokenProgram.Transfer(Pk(2), Pk(3), Pk(6), 1, null, []);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
        }

        [Test]
        public void ElevenMembers_IsAccepted()
        {
            // Arrange
            var members = Enumerable.Range(10, 11).Select(value => Pk((byte)value)).ToArray();

            // Act
            var instruction = TokenProgram.Transfer(Pk(2), Pk(3), Pk(6), 1, null, members);

            // Assert
            instruction.Accounts.Should().HaveCount(14);
        }

        [Test]
        public void TwelveMembers_Throws()
        {
            // Arrange
            var members = Enumerable.Range(10, 12).Select(value => Pk((byte)value)).ToArray();

            // Act
            Action act = () => _ = TokenProgram.Transfer(Pk(2), Pk(3), Pk(6), 1, null, members);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithParameterName("multisigSigners")
                .WithMessage("*at most 11*12*");
        }

        private static void CheckMultisig(Instruction instruction, int authorityIndex, PublicKey authority)
        {
            instruction.Accounts.Should().HaveCount(authorityIndex + 1 + Members.Length);
            Check(instruction.Accounts[authorityIndex], authority, signer: false, writable: false);
            Check(instruction.Accounts[authorityIndex + 1], Members[0], signer: true, writable: false);
            Check(instruction.Accounts[authorityIndex + 2], Members[1], signer: true, writable: false);
        }
    }
}
