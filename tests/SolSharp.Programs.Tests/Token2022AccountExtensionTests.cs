using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022AccountExtensionTests
{
    private static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    [TestFixture]
    public sealed class InitializeDefaultAccountState
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.InitializeDefaultAccountState(Key(1), DefaultTokenAccountState.Frozen);

            // Assert
            Hex(instruction).Should().Be("1c0002");
            Metas(instruction).Should().Equal((Key(1), false, true));
        }

        [Test]
        public void UndefinedStateIsRejected()
        {
            // Act
            Action act = static () => _ = Token2022Program.InitializeDefaultAccountState(
                Key(1), (DefaultTokenAccountState)255);

            // Assert
            act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("state");
        }
    }

    [TestFixture]
    public sealed class UpdateDefaultAccountState
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.UpdateDefaultAccountState(
                Key(1),
                Key(2),
                DefaultTokenAccountState.Initialized,
                [Key(3), Key(4)]);

            // Assert
            Hex(instruction).Should().Be("1c0101");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), true, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class EnableRequiredTransferMemos
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.EnableRequiredTransferMemos(Key(1), Key(2));

            // Assert
            Hex(instruction).Should().Be("1e00");
            Metas(instruction).Should().Equal((Key(1), false, true), (Key(2), true, false));
        }
    }

    [TestFixture]
    public sealed class DisableRequiredTransferMemos
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.DisableRequiredTransferMemos(Key(1), Key(2))).Should().Be("1e01");
    }

    [TestFixture]
    public sealed class EnableCpiGuard
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.EnableCpiGuard(Key(1), Key(2))).Should().Be("2200");
    }

    [TestFixture]
    public sealed class DisableCpiGuard
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.DisableCpiGuard(Key(1), Key(2), [Key(3)]);

            // Assert
            Hex(instruction).Should().Be("2201");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), true, false));
        }
    }
}
