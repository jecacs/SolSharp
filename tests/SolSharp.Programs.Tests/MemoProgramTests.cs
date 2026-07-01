using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MemoProgramTests
{
    private const string MemoProgramId = "MemoSq4gqABAXKb96qnH8TysNcWxMyWCqXgDLGmfcHr";

    private static PublicKey Pk(byte value)
    {
        var bytes = new byte[PublicKey.Length];
        Array.Fill(bytes, value);
        return new PublicKey(bytes);
    }

    [TestFixture]
    public sealed class Memo
    {
        // Reference from the Rust spl-memo builder (build_memo): program MemoSq4..., data is the UTF-8
        // text, each signer is a read-only signer (AccountMeta::new_readonly(pubkey, true)).
        [Test]
        public void WithSigner_MatchesSplMemo()
        {
            // Act
            var instruction = MemoProgram.Memo("hello", Pk(6));

            // Assert
            instruction.ProgramId.Should().Be(PublicKey.Parse(MemoProgramId));
            Convert.ToHexString(instruction.Data).ToLowerInvariant().Should().Be("68656c6c6f");
            instruction.Accounts.Should().ContainSingle();
            instruction.Accounts[0].PublicKey.Should().Be(Pk(6));
            instruction.Accounts[0].IsSigner.Should().BeTrue();
            instruction.Accounts[0].IsWritable.Should().BeFalse();
        }

        [Test]
        public void WithoutSigners_HasNoAccounts()
        {
            // Act
            var instruction = MemoProgram.Memo("hello");

            // Assert
            instruction.Accounts.Should().BeEmpty();
            Convert.ToHexString(instruction.Data).ToLowerInvariant().Should().Be("68656c6c6f");
        }
    }
}
