using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
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
            // Act
            var instruction = TokenProgram.Transfer(Key(4), Key(5), Key(6), 1000);

            // Assert
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
            // Act
            var instruction = TokenProgram.TransferChecked(Key(4), Key(3), Key(5), Key(6), 1000, 6);

            // Assert
            instruction.ProgramId.Should().Be(TokenProgram.ProgramId);
            instruction.Data.Should().Equal(Hex("0ce80300000000000006"));
            instruction.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable)).Should().Equal(
                (Key(4), false, true),
                (Key(3), false, false),
                (Key(5), false, true),
                (Key(6), true, false));
        }
    }

    [TestFixture]
    public sealed class TokenProgramOverride
    {
        private static PublicKey Token2022 => PublicKey.Parse(SolanaProgramIds.Token2022Program);

        [Test]
        public void TransferChecked_TargetsGivenProgram_WithIdenticalLayout()
        {
            // Act
            var classic = TokenProgram.TransferChecked(Key(4), Key(3), Key(5), Key(6), 1000, 6);
            var extended = TokenProgram.TransferChecked(Key(4), Key(3), Key(5), Key(6), 1000, 6, Token2022);

            // Assert
            classic.ProgramId.Should().Be(TokenProgram.ProgramId); // default stays classic SPL Token
            extended.ProgramId.Should().Be(Token2022);
            extended.Data.Should().Equal(classic.Data);
            extended.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable))
                .Should().Equal(classic.Accounts.Select(a => (a.PublicKey, a.IsSigner, a.IsWritable)));
        }

        [Test]
        public void MintTo_TargetsGivenProgram_WithIdenticalLayout()
        {
            // Act
            var extended = TokenProgram.MintTo(Key(1), Key(2), Key(3), 500, Token2022);

            // Assert
            extended.ProgramId.Should().Be(Token2022);
            extended.Data.Should().Equal(TokenProgram.MintTo(Key(1), Key(2), Key(3), 500).Data);
        }
    }
}
