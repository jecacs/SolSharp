using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class TokenProgramParityTests
{
    private static readonly PublicKey Rent = PublicKey.Parse(Sysvars.Rent);

    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class InitializeMint2
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.InitializeMint2(Key(1), 6, Key(2), Key(3));

            // Assert
            Hex(instruction).Should().Be(
                "1406" +
                "0202020202020202020202020202020202020202020202020202020202020202" +
                "01" +
                "0303030303030303030303030303030303030303030303030303030303030303");
            Metas(instruction).Should().Equal((Key(1), false, true));
        }
    }

    [TestFixture]
    public sealed class InitializeAccount2
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.InitializeAccount2(Key(1), Key(2), Key(3));

            // Assert
            Hex(instruction).Should().Be("10" +
                "0303030303030303030303030303030303030303030303030303030303030303");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Rent, false, false));
        }
    }

    [TestFixture]
    public sealed class InitializeAccount3
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.InitializeAccount3(Key(1), Key(2), Key(3));

            // Assert
            Hex(instruction).Should().Be("12" +
                "0303030303030303030303030303030303030303030303030303030303030303");
            Metas(instruction).Should().Equal((Key(1), false, true), (Key(2), false, false));
        }
    }

    [TestFixture]
    public sealed class InitializeMultisig
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Arrange
            PublicKey[] members = [Key(2), Key(3), Key(4)];

            // Act
            var instruction = TokenProgram.InitializeMultisig(Key(1), members, 2);

            // Assert
            Hex(instruction).Should().Be("0202");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Rent, false, false),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), false, false));
        }

        [TestCase(0, 1)]
        [TestCase(12, 1)]
        [TestCase(1, 0)]
        [TestCase(1, 2)]
        public void RejectsUnrepresentableThresholds(int signerCount, byte required)
        {
            // Arrange
            var members = Enumerable.Range(0, signerCount).Select(index => Key((byte)(index + 2))).ToArray();

            // Act
            Action act = () => _ = TokenProgram.InitializeMultisig(Key(1), members, required);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }

    [TestFixture]
    public sealed class InitializeMultisig2
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Arrange
            PublicKey[] members = [Key(2), Key(3), Key(4)];

            // Act
            var instruction = TokenProgram.InitializeMultisig2(Key(1), members, 2);

            // Assert
            Hex(instruction).Should().Be("1302");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), false, false));
        }
    }

    [TestFixture]
    public sealed class SyncNativeWithRentSysvar
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.SyncNativeWithRentSysvar(Key(1));

            // Assert
            Hex(instruction).Should().Be("11");
            Metas(instruction).Should().Equal((Key(1), false, true), (Rent, false, false));
        }
    }

    [TestFixture]
    public sealed class GetAccountDataSize
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.GetAccountDataSize(Key(2));

            // Assert
            Hex(instruction).Should().Be("15");
            Metas(instruction).Should().Equal((Key(2), false, false));
        }
    }

    [TestFixture]
    public sealed class InitializeImmutableOwner
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.InitializeImmutableOwner(Key(1));

            // Assert
            Hex(instruction).Should().Be("16");
            Metas(instruction).Should().Equal((Key(1), false, true));
        }
    }

    [TestFixture]
    public sealed class AmountToUiAmount
    {
        [Test]
        public void MatchesPinnedTokenInterface() =>
            Hex(TokenProgram.AmountToUiAmount(Key(2), 1_000)).Should().Be("17e803000000000000");
    }

    [TestFixture]
    public sealed class UiAmountToAmount
    {
        [Test]
        public void MatchesPinnedTokenInterface() =>
            Hex(TokenProgram.UiAmountToAmount(Key(2), "1.25")).Should().Be("18312e3235");

        [Test]
        public void RejectsInvalidUnicode()
        {
            // Act
            Action act = () => _ = TokenProgram.UiAmountToAmount(Key(1), "\ud800");

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("uiAmount");
        }
    }

    [TestFixture]
    public sealed class WithdrawExcessLamports
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var instruction = TokenProgram.WithdrawExcessLamports(Key(1), Key(2), Key(3));

            // Assert
            Hex(instruction).Should().Be("26");
            instruction.ProgramId.Should().Be(Token2022Program.ProgramId);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class UnwrapLamports
    {
        [Test]
        public void MatchesPinnedTokenInterface()
        {
            // Act
            var unwrapAll = TokenProgram.UnwrapLamports(Key(1), Key(2), Key(3));
            var unwrapSome = TokenProgram.UnwrapLamports(Key(1), Key(2), Key(3), 42);

            // Assert
            Hex(unwrapAll).Should().Be("2d00");
            Hex(unwrapSome).Should().Be("2d012a00000000000000");
            unwrapAll.ProgramId.Should().Be(Token2022Program.ProgramId);
            unwrapSome.ProgramId.Should().Be(Token2022Program.ProgramId);
        }
    }

    [TestFixture]
    public sealed class Batch
    {
        [Test]
        public void MatchesPinnedTokenInterfaceEncoding()
        {
            // Arrange
            var sync = TokenProgram.SyncNative(Key(1), Token2022Program.ProgramId);
            var transfer = TokenProgram.Transfer(Key(2), Key(3), Key(4), 5, Token2022Program.ProgramId);

            // Act
            var batch = TokenProgram.Batch([sync, transfer]);

            // Assert
            Hex(batch).Should().Be("ff0101110309030500000000000000");
            Metas(batch).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), false, true),
                (Key(4), true, false));
            batch.ProgramId.Should().Be(Token2022Program.ProgramId);
        }

        [Test]
        public void RejectsAnotherProgram()
        {
            // Arrange
            var systemInstruction = SystemProgram.Transfer(Key(1), Key(2), 1);

            // Act
            Action act = () => _ = TokenProgram.Batch([systemInstruction]);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("instructions");
        }

        [Test]
        public void RejectsEmptyDataEmptyBatchAndNestedBatches()
        {
            // Arrange
            var inner = TokenProgram.Batch([TokenProgram.SyncNative(Key(1), Token2022Program.ProgramId)]);
            var emptyData = new Instruction
            {
                ProgramId = Token2022Program.ProgramId,
                Accounts = [],
                Data = []
            };

            // Act
            Action empty = () => _ = TokenProgram.Batch([]);
            Action emptyInnerData = () => _ = TokenProgram.Batch([emptyData]);
            Action nested = () => _ = TokenProgram.Batch([inner]);

            // Assert
            empty.Should().Throw<ArgumentException>().WithParameterName("instructions");
            emptyInnerData.Should().Throw<ArgumentException>().WithParameterName("instructions");
            nested.Should().Throw<ArgumentException>().WithParameterName("instructions");
        }
    }
}
