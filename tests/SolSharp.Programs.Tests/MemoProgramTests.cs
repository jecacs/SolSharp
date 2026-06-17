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
        // Reference from solana-py spl.memo.create_memo: program MemoSq4..., data is the UTF-8 text,
        // the signer is a writable signer.
        [Test]
        public void WithSigner_MatchesSolanaPy()
        {
            var instruction = MemoProgram.Memo("hello", Pk(6));

            instruction.ProgramId.Should().Be(PublicKey.Parse(MemoProgramId));
            Convert.ToHexString(instruction.Data).ToLowerInvariant().Should().Be("68656c6c6f");
            instruction.Accounts.Should().ContainSingle();
            instruction.Accounts[0].PublicKey.Should().Be(Pk(6));
            instruction.Accounts[0].IsSigner.Should().BeTrue();
            instruction.Accounts[0].IsWritable.Should().BeTrue();
        }

        [Test]
        public void WithoutSigners_HasNoAccounts()
        {
            var instruction = MemoProgram.Memo("hello");

            instruction.Accounts.Should().BeEmpty();
            Convert.ToHexString(instruction.Data).ToLowerInvariant().Should().Be("68656c6c6f");
        }
    }
}
