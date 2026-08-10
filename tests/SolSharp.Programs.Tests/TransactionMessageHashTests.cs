using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class TransactionMessageHashTests
{
    [TestFixture]
    public sealed class Compute
    {
        [Test]
        public void PinnedSolanaSdkVector_MatchesDomainSeparatedBlake3Hash()
        {
            // Arrange: solana-sdk/message/src/legacy.rs test_message_hash at the pinned commit.
            var program0 = PublicKey.Parse("4uQeVj5tqViQh7yWWGStvkEG1Zmhx6uasJtWCJziofM");
            var program1 = PublicKey.Parse("8opHzTAnfzRpPEx21XtnrVTX28YQuCpAjcn1PczScKh");
            var id0 = PublicKey.Parse("CiDwVBFgWV9E5MvXWoLgnEgn2hK7rJikbvfWavzAQz3");
            var id1 = PublicKey.Parse("GcdayuLaLyrdmUu324nahyv33G5poQdLUEZ1nEytDeP");
            var id2 = PublicKey.Parse("LX3EUdRUBUa3TbsYXLEUdj9J3prXkWXvLYSWyYyc2Jj");
            var id3 = PublicKey.Parse("QRSsyMWN1yHT9ir42bgNZUNZ4PdEhcSWCrL2AryKpy5");
            var instructions = new[]
            {
                new Instruction { ProgramId = program0, Accounts = [AccountMeta.Writable(id0)], Data = new byte[4] },
                new Instruction { ProgramId = program0, Accounts = [AccountMeta.WritableSigner(id1)], Data = new byte[4] },
                new Instruction { ProgramId = program1, Accounts = [AccountMeta.Readonly(id2)], Data = new byte[4] },
                new Instruction { ProgramId = program1, Accounts = [AccountMeta.ReadonlySigner(id3)], Data = new byte[4] }
            };
            var message = Message.Compile(id1, default(Hash), instructions);

            // Act
            var fromMessage = TransactionMessageHash.Compute(message);
            var fromBytes = TransactionMessageHash.Compute(message.Serialize());

            // Assert
            fromMessage.Should().Be(Hash.Parse("7VWCF4quo2CcWQFNUayZiorxpiR5ix8YzLebrXKf3fMF"));
            fromBytes.Should().Be(fromMessage);
        }

        [Test]
        public void NullMessage_Throws()
        {
            // Act
            Action act = static () => TransactionMessageHash.Compute((ITransactionMessage)null!);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("message");
        }
    }
}
