using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class AssociatedTokenAccountTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    [TestFixture]
    public sealed class GetAddress
    {
        // Reference from solders: associated token address for owner=[1;32], mint=[2;32], SPL Token program.
        [Test]
        public void MatchesSolanaSdk()
        {
            // Act
            var ata = AssociatedTokenAccount.GetAddress(Key(1), Key(2));

            // Assert
            ata.Should().Be(PublicKey.Parse("CsYkfSfTUTWwnoeRkGchtai5kkYz2SC33kKJwA99wVr3"));
        }
    }

    [TestFixture]
    public sealed class Create
    {
        // Modern 6-account form (payer, ata, owner, mint, system, token). solana-py also appends the
        // deprecated Rent sysvar; the current program does not require it. The ATA address itself is the
        // solders-derived associated token account for owner=[2], mint=[3].
        [Test]
        public void ProducesExpectedAccountsAndAddress()
        {
            // Arrange
            var payer = Key(1);
            var owner = Key(2);
            var mint = Key(3);
            var ata = PublicKey.Parse("BKbxqhBJfLZNgac5dEUesF1V5xRZSzxDkcpQBAy4c8sw");

            // Act
            var instruction = AssociatedTokenAccount.Create(payer, owner, mint);

            // Assert
            instruction.ProgramId.Should().Be(AssociatedTokenAccount.ProgramId);
            instruction.Data.Should().BeEmpty();
            instruction.Accounts.Should().HaveCount(6);

            instruction.Accounts[0].PublicKey.Should().Be(payer);
            instruction.Accounts[0].IsSigner.Should().BeTrue();
            instruction.Accounts[0].IsWritable.Should().BeTrue();

            instruction.Accounts[1].PublicKey.Should().Be(ata);
            instruction.Accounts[1].IsSigner.Should().BeFalse();
            instruction.Accounts[1].IsWritable.Should().BeTrue();

            instruction.Accounts[2].PublicKey.Should().Be(owner);
            instruction.Accounts[3].PublicKey.Should().Be(mint);
            instruction.Accounts[4].PublicKey.Should().Be(SystemProgram.ProgramId);
            instruction.Accounts[5].PublicKey.Should().Be(TokenProgram.ProgramId);
            instruction.Accounts.Skip(2).Should().OnlyContain(a => !a.IsSigner && !a.IsWritable);
        }
    }

    [TestFixture]
    public sealed class CreateIdempotent
    {
        // Reference from solana-py create_idempotent_associated_token_account: identical accounts to Create,
        // with the one-byte instruction tag 1.
        [Test]
        public void MatchesCreateWithIdempotentTag()
        {
            // Arrange
            var payer = Key(1);
            var owner = Key(2);
            var mint = Key(3);

            // Act
            var create = AssociatedTokenAccount.Create(payer, owner, mint);
            var idempotent = AssociatedTokenAccount.CreateIdempotent(payer, owner, mint);

            // Assert
            idempotent.Data.Should().Equal((byte)1);
            idempotent.ProgramId.Should().Be(create.ProgramId);
            idempotent.Accounts.Should().Equal(create.Accounts);
        }
    }
}
