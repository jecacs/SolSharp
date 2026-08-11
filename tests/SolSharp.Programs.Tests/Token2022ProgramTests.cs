using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022ProgramTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class GetAccountDataSize
    {
        [Test]
        public void MatchesPinnedToken2022Interface()
        {
            // Act
            var instruction = Token2022Program.GetAccountDataSize(
                Key(1),
                [Token2022ExtensionType.TransferFeeConfig, Token2022ExtensionType.MemoTransfer]);

            // Assert
            instruction.ProgramId.Should().Be(PublicKey.Parse(SolanaProgramIds.Token2022Program));
            Hex(instruction).Should().Be("1501000800");
            Metas(instruction).Should().Equal((Key(1), false, false));
        }

        [Test]
        public void RejectsUnknownExtensionType()
        {
            // Act
            Action act = static () => _ = Token2022Program.GetAccountDataSize(
                Key(1), [(Token2022ExtensionType)ushort.MaxValue]);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("extensionTypes");
        }
    }

    [TestFixture]
    public sealed class InitializeMintCloseAuthority
    {
        [Test]
        public void MatchesPinnedToken2022Interface()
        {
            // Act
            var none = Token2022Program.InitializeMintCloseAuthority(Key(1), null);
            var some = Token2022Program.InitializeMintCloseAuthority(Key(1), Key(2));

            // Assert
            Hex(none).Should().Be("1900");
            Hex(some).Should().Be(
                "1901" + "0202020202020202020202020202020202020202020202020202020202020202");
            Metas(some).Should().Equal((Key(1), false, true));
        }
    }

    [TestFixture]
    public sealed class Reallocate
    {
        [Test]
        public void MatchesPinnedToken2022Interface()
        {
            // Act
            var instruction = Token2022Program.Reallocate(
                Key(1),
                Key(2),
                Key(3),
                [Token2022ExtensionType.MemoTransfer, Token2022ExtensionType.CpiGuard]);

            // Assert
            Hex(instruction).Should().Be("1d08000b00");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), true, true),
                (SystemProgram.ProgramId, false, false),
                (Key(3), true, false));
        }

        [Test]
        public void SupportsMultisigOwner()
        {
            // Act
            var instruction = Token2022Program.Reallocate(
                Key(1),
                Key(2),
                Key(3),
                [Token2022ExtensionType.MemoTransfer],
                [Key(4), Key(5)]);

            // Assert
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), true, true),
                (SystemProgram.ProgramId, false, false),
                (Key(3), false, false),
                (Key(4), true, false),
                (Key(5), true, false));
        }
    }

    [TestFixture]
    public sealed class CreateNativeMint
    {
        [Test]
        public void MatchesPinnedToken2022Interface()
        {
            // Act
            var instruction = Token2022Program.CreateNativeMint(Key(1));

            // Assert
            Hex(instruction).Should().Be("1f");
            Metas(instruction).Should().Equal(
                (Key(1), true, true),
                (PublicKey.Parse(Mints.Token2022NativeMint), false, true),
                (SystemProgram.ProgramId, false, false));
        }

        [Test]
        public void UsesTheDerivedToken2022NativeMintNotWrappedSol()
        {
            // Arrange: the pinned interface defines the native mint as a program address of
            // ["native-mint", 255] under Token-2022, so derive it instead of trusting the constant.
            byte[][] seeds = ["native-mint"u8.ToArray(), [255]];

            // Act
            var created = ProgramDerivedAddress.TryCreateProgramAddress(
                seeds,
                Token2022Program.ProgramId,
                out var derived);
            var instruction = Token2022Program.CreateNativeMint(Key(1));

            // Assert
            created.Should().BeTrue();
            derived.Should().Be(PublicKey.Parse(Mints.Token2022NativeMint));
            instruction.Accounts[1].PublicKey.Should().Be(derived);
            instruction.Accounts[1].PublicKey.Should().NotBe(PublicKey.Parse(Mints.WrappedSol));
        }
    }

    [TestFixture]
    public sealed class InitializeNonTransferableMint
    {
        [Test]
        public void MatchesPinnedToken2022Interface() =>
            Hex(Token2022Program.InitializeNonTransferableMint(Key(2))).Should().Be("20");
    }

    [TestFixture]
    public sealed class InitializePermanentDelegate
    {
        [Test]
        public void MatchesPinnedToken2022Interface() =>
            Hex(Token2022Program.InitializePermanentDelegate(Key(2), Key(3))).Should().Be(
                "23" + "0303030303030303030303030303030303030303030303030303030303030303");
    }
}
