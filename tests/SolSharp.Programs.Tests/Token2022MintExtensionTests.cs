using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022MintExtensionTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    private static void AssertPointerInitializer(
        Func<PublicKey, PublicKey?, PublicKey?, Instruction> build,
        byte outerDiscriminator)
    {
        // Act
        var instruction = build(Key(1), Key(2), Key(3));

        // Assert
        Hex(instruction).Should().Be(
            outerDiscriminator.ToString("x2") + "00" +
            "0202020202020202020202020202020202020202020202020202020202020202" +
            "0303030303030303030303030303030303030303030303030303030303030303");
        Metas(instruction).Should().Equal((Key(1), false, true));
    }

    [TestFixture]
    public sealed class InitializeInterestBearingMint
    {
        [Test]
        public void MatchesPinnedInterface() =>
            // Act & Assert
            Hex(Token2022Program.InitializeInterestBearingMint(Key(1), Key(2), -25)).Should().Be(
                "2100" + "0202020202020202020202020202020202020202020202020202020202020202" + "e7ff");
    }

    [TestFixture]
    public sealed class UpdateInterestRate
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.UpdateInterestRate(Key(1), Key(2), 250, [Key(3)]);

            // Assert
            Hex(instruction).Should().Be("2101fa00");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(3), true, false));
        }
    }

    [TestFixture]
    public sealed class InitializeTransferHook
    {
        [Test]
        public void MatchesPinnedInterface() =>
            AssertPointerInitializer(Token2022Program.InitializeTransferHook, 36);
    }

    [TestFixture]
    public sealed class InitializeMetadataPointer
    {
        [Test]
        public void MatchesPinnedInterface() =>
            AssertPointerInitializer(Token2022Program.InitializeMetadataPointer, 39);

        [Test]
        public void MaybeNullRejectsAmbiguousZeroAddress()
        {
            // Act
            Action act = () => _ = Token2022Program.InitializeMetadataPointer(Key(1), default(PublicKey), Key(2));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("authority");
        }
    }

    [TestFixture]
    public sealed class InitializeGroupPointer
    {
        [Test]
        public void MatchesPinnedInterface() =>
            AssertPointerInitializer(Token2022Program.InitializeGroupPointer, 40);
    }

    [TestFixture]
    public sealed class InitializeGroupMemberPointer
    {
        [Test]
        public void MatchesPinnedInterface() =>
            AssertPointerInitializer(Token2022Program.InitializeGroupMemberPointer, 41);
    }

    [TestFixture]
    public sealed class UpdateMetadataPointer
    {
        [Test]
        public void MatchesPinnedMultisigLayout()
        {
            // Act
            var instruction = Token2022Program.UpdateMetadataPointer(Key(1), Key(2), Key(3), [Key(4), Key(5)]);

            // Assert
            Hex(instruction).Should().Be(
                "2701" + "0303030303030303030303030303030303030303030303030303030303030303");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, false),
                (Key(4), true, false),
                (Key(5), true, false));
        }
    }

    [TestFixture]
    public sealed class InitializeScaledUiAmount
    {
        [Test]
        public void MatchesPinnedPodLayout() =>
            Hex(Token2022Program.InitializeScaledUiAmount(Key(1), Key(2), 1.5)).Should().Be(
                "2b00" + "0202020202020202020202020202020202020202020202020202020202020202" +
                "000000000000f83f");
    }

    [TestFixture]
    public sealed class UpdateScaledUiAmount
    {
        [Test]
        public void MatchesPinnedPodLayout() =>
            Hex(Token2022Program.UpdateScaledUiAmount(Key(1), Key(2), 2.0, 42))
                .Should().Be("2b0100000000000000402a00000000000000");
    }

    [TestFixture]
    public sealed class InitializePausableMint
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.InitializePausableMint(Key(1), Key(2))).Should().Be(
                "2c00" + "0202020202020202020202020202020202020202020202020202020202020202");
    }

    [TestFixture]
    public sealed class PauseMint
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.PauseMint(Key(1), Key(2))).Should().Be("2c01");
    }

    [TestFixture]
    public sealed class ResumeMint
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.ResumeMint(Key(1), Key(2))).Should().Be("2c02");
    }

    [TestFixture]
    public sealed class InitializePermissionedBurn
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.InitializePermissionedBurn(Key(1), Key(2))).Should().Be(
                "2e00" + "0202020202020202020202020202020202020202020202020202020202020202");
    }

    [TestFixture]
    public sealed class PermissionedBurn
    {
        [Test]
        public void MatchesPinnedInterface()
        {
            // Act
            var instruction = Token2022Program.PermissionedBurn(Key(1), Key(2), Key(3), Key(4), 42);

            // Assert
            Hex(instruction).Should().Be("2e012a00000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(3), true, false),
                (Key(4), true, false));
        }
    }

    [TestFixture]
    public sealed class PermissionedBurnChecked
    {
        [Test]
        public void MatchesPinnedInterface() =>
            Hex(Token2022Program.PermissionedBurnChecked(Key(1), Key(2), Key(3), Key(4), 42, 6))
                .Should().Be("2e022a0000000000000006");
    }
}
