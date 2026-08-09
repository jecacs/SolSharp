using FluentAssertions;
using NUnit.Framework;
using static SolSharp.Programs.Tests.PrecompileProgramTestHelpers;

namespace SolSharp.Programs.Tests;

internal static class PrecompileProgramTestHelpers
{
    internal static string Repeat(byte value, int count) =>
        string.Concat(Enumerable.Repeat(value.ToString("x2"), count));

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();
}

public static class Ed25519ProgramTests
{
    [TestFixture]
    public sealed class CreateInstruction
    {
        [Test]
        public void SelfContainedInstruction_MatchesPinnedRustLayout()
        {
            // Act
            var instruction = Ed25519Program.CreateInstruction(
                [0xaa, 0xbb],
                Enumerable.Repeat((byte)0x22, Ed25519Program.SignatureLength).ToArray(),
                Enumerable.Repeat((byte)0x11, Ed25519Program.PublicKeyLength).ToArray());

            // Assert
            Hex(instruction).Should().Be(
                "0100" +
                "3000ffff1000ffff70000200ffff" +
                Repeat(0x11, 32) +
                Repeat(0x22, 64) +
                "aabb");
            instruction.Accounts.Should().BeEmpty();
            Ed25519Program.DecodeOffsets(instruction.Data).Should().Equal(
                new Ed25519SignatureOffsets(48, ushort.MaxValue, 16, ushort.MaxValue, 112, 2, ushort.MaxValue));
        }
    }

    [TestFixture]
    public sealed class CreateOffsetsInstruction
    {
        [Test]
        public void OffsetsOnly_RoundTripsExactLittleEndianRecord()
        {
            // Arrange
            var offsets = new Ed25519SignatureOffsets(1, 2, 3, 4, 5, 6, 7);

            // Act
            var instruction = Ed25519Program.CreateOffsetsInstruction([offsets]);

            // Assert
            Hex(instruction).Should().Be("01000100020003000400050006000700");
            Ed25519Program.DecodeOffsets(instruction.Data).Should().Equal(offsets);
        }

        [Test]
        public void CountBeyondRuntimeWidth_IsRejected()
        {
            // Arrange
            var tooMany = Enumerable.Repeat(default(Ed25519SignatureOffsets), byte.MaxValue + 1).ToArray();

            // Act
            var act = () => Ed25519Program.CreateOffsetsInstruction(tooMany);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class DecodeOffsets
    {
        [Test]
        public void PaddingIsIgnoredAndZeroCountWithTrailingDataIsRejected()
        {
            // Arrange
            var oneRecordWithIgnoredPadding = Convert.FromHexString("01ff0100020003000400050006000700");

            // Act
            var decoded = Ed25519Program.DecodeOffsets(oneRecordWithIgnoredPadding);
            var zeroWithTrailingData = () => Ed25519Program.DecodeOffsets([0, 0, 0]);

            // Assert
            decoded.Should().Equal(new Ed25519SignatureOffsets(1, 2, 3, 4, 5, 6, 7));
            zeroWithTrailingData.Should().Throw<ArgumentException>();
        }
    }
}

public static class Secp256r1ProgramTests
{
    [TestFixture]
    public sealed class CreateInstruction
    {
        [Test]
        public void SelfContainedInstruction_MatchesPinnedRustLayout()
        {
            // Act
            var instruction = Secp256r1Program.CreateInstruction(
                [0xaa, 0xbb],
                Enumerable.Repeat((byte)0x22, Secp256r1Program.SignatureLength).ToArray(),
                Enumerable.Repeat((byte)0x11, Secp256r1Program.CompressedPublicKeyLength).ToArray());

            // Assert
            Hex(instruction).Should().Be(
                "0100" +
                "3100ffff1000ffff71000200ffff" +
                Repeat(0x11, 33) +
                Repeat(0x22, 64) +
                "aabb");
            Secp256r1Program.DecodeOffsets(instruction.Data).Should().Equal(
                new Secp256r1SignatureOffsets(49, ushort.MaxValue, 16, ushort.MaxValue, 113, 2, ushort.MaxValue));
        }
    }

    [TestFixture]
    public sealed class CreateOffsetsInstruction
    {
        [Test]
        public void EmptyAndTooManyRecords_AreRejected()
        {
            // Arrange
            var tooMany = Enumerable.Repeat(default(Secp256r1SignatureOffsets), 9).ToArray();

            // Act
            var createEmpty = () => Secp256r1Program.CreateOffsetsInstruction([]);
            var createTooMany = () => Secp256r1Program.CreateOffsetsInstruction(tooMany);

            // Assert
            createEmpty.Should().Throw<ArgumentException>();
            createTooMany.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class DecodeOffsets
    {
        [Test]
        public void PaddingSemantics_MatchRuntime()
        {
            // Arrange
            var oneRecordWithIgnoredPadding = Convert.FromHexString("01ff0100020003000400050006000000");

            // Act
            var decoded = Secp256r1Program.DecodeOffsets(oneRecordWithIgnoredPadding);

            // Assert
            decoded.Should().Equal(new Secp256r1SignatureOffsets(1, 2, 3, 4, 5, 6, 0));
        }
    }
}

public static class Secp256k1ProgramTests
{
    [TestFixture]
    public sealed class CreateInstruction
    {
        [Test]
        public void SelfContainedInstruction_MatchesPinnedRustLayout()
        {
            // Act
            var instruction = Secp256k1Program.CreateInstruction(
                [0xaa, 0xbb],
                Enumerable.Repeat((byte)0x22, Secp256k1Program.SignatureLength).ToArray(),
                1,
                Enumerable.Repeat((byte)0x11, Secp256k1Program.EthereumAddressLength).ToArray());

            // Assert
            Hex(instruction).Should().Be(
                "01" +
                "2000000c00006100020000" +
                Repeat(0x11, 20) +
                Repeat(0x22, 64) +
                "01aabb");
            Secp256k1Program.DecodeOffsets(instruction.Data).Should().Equal(
                new Secp256k1SignatureOffsets(32, 0, 12, 0, 97, 2, 0));
        }
    }

    [TestFixture]
    public sealed class CreateOffsetsInstruction
    {
        [Test]
        public void OffsetsOnly_RoundTripsMixedWidthRecord()
        {
            // Arrange
            var offsets = new Secp256k1SignatureOffsets(1, 2, 3, 4, 5, 6, 7);

            // Act
            var instruction = Secp256k1Program.CreateOffsetsInstruction([offsets]);

            // Assert
            Hex(instruction).Should().Be("010100020300040500060007");
            Secp256k1Program.DecodeOffsets(instruction.Data).Should().Equal(offsets);
        }
    }

    [TestFixture]
    public sealed class DecodeOffsets
    {
        [Test]
        public void ZeroCountWithTrailingData_IsRejectedLikeTheRuntime()
        {
            // Act
            var act = () => Secp256k1Program.DecodeOffsets([0, 0]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }
}
