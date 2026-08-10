using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.LoaderProgramTestHelpers;

namespace SolSharp.Programs.Tests;

public static class LegacyBpfLoaderProgramTests
{
    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void MatchesLoaderV2Bincode()
        {
            // Act
#pragma warning disable CS0618
            var instruction = LegacyBpfLoaderProgram.Write(Pk(1), 0x12345678, [0xaa, 0xbb, 0xcc]);
#pragma warning restore CS0618

            // Assert
            Hex(instruction).Should().Be("00000000785634120300000000000000aabbcc");
            instruction.ProgramId.Should().Be(LegacyBpfLoaderProgram.ProgramId);
            instruction.Accounts.Should().ContainSingle()
                .Which.Should().Match<AccountMeta>(static account => account.IsSigner && account.IsWritable);
        }
    }

    [TestFixture]
    public sealed class Finalize
    {
        [Test]
        public void MatchesLoaderV2Bincode() =>
#pragma warning disable CS0618
            Hex(LegacyBpfLoaderProgram.Finalize(Pk(1))).Should().Be("01000000");
#pragma warning restore CS0618

    }
}

public static class UpgradeableBpfLoaderProgramTests
{
    [TestFixture]
    public sealed class GetProgramDataAddress
    {
        [Test]
        public void DerivesCanonicalProgramDataPda()
        {
            // Arrange
            var program = Pk(9);
            var expected = ProgramDerivedAddress.FindProgramAddress(
                [program.ToBytes()], UpgradeableBpfLoaderProgram.ProgramId).Address;

            // Act
            var address = UpgradeableBpfLoaderProgram.GetProgramDataAddress(program);

            // Assert
            address.Should().Be(expected);
        }
    }

    [TestFixture]
    public sealed class CreateBuffer
    {
        [Test]
        public void ComposesPinnedCreateAndInitializeInstructions()
        {
            // Arrange
            const ulong lamports = 123;
            const ulong programLength = 456;
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(1),
                Pk(2),
                lamports,
                checked(UpgradeableBpfLoaderState.BufferMetadataLength + programLength),
                UpgradeableBpfLoaderProgram.ProgramId);
            var expectedInitialize = UpgradeableBpfLoaderProgram.InitializeBuffer(Pk(2), Pk(3));

            // Act
            var instructions = UpgradeableBpfLoaderProgram.CreateBuffer(
                Pk(1), Pk(2), Pk(3), lamports, programLength);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedInitialize);
        }

        [Test]
        public void MetadataLengthOverflow_Throws()
        {
            // Act
            Action act = static () => UpgradeableBpfLoaderProgram.CreateBuffer(
                Pk(1), Pk(2), Pk(3), 1, ulong.MaxValue);

            // Assert
            act.Should().Throw<OverflowException>();
        }
    }

    [TestFixture]
    public sealed class InitializeBuffer
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.InitializeBuffer(Pk(1), Pk(2))).Should().Be("00000000");
    }

    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.Write(Pk(1), Pk(2), 0x12345678, [0xaa, 0xbb, 0xcc]))
                .Should().Be("01000000785634120300000000000000aabbcc");
    }

    [TestFixture]
    public sealed class DeployInstruction
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.DeployInstruction(Pk(1), Pk(2), Pk(3), Pk(4), 42))
                .Should().Be("020000002a0000000000000001");
    }

    [TestFixture]
    public sealed class Upgrade
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.Upgrade(Pk(1), Pk(2), Pk(3), Pk(4), false))
                .Should().Be("0300000000");
    }

    [TestFixture]
    public sealed class SetBufferAuthority
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.SetBufferAuthority(Pk(1), Pk(2), Pk(3)))
                .Should().Be("04000000");
    }

    [TestFixture]
    public sealed class Close
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.Close(Pk(1), Pk(2), Pk(3), Pk(4), true))
                .Should().Be("0500000001");
    }

    [TestFixture]
    public sealed class ExtendProgram
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.ExtendProgram(Pk(1), 10_240))
                .Should().Be("0600000000280000");
    }

    [TestFixture]
    public sealed class SetBufferAuthorityChecked
    {
        [Test]
        public void MatchesPinnedWincodeBincodeCompatibilityVector() =>
            Hex(UpgradeableBpfLoaderProgram.SetBufferAuthorityChecked(Pk(1), Pk(2), Pk(3)))
                .Should().Be("07000000");
    }

    [TestFixture]
    public sealed class DeployWithMaximumProgramLength
    {
        [Test]
        public void ComposesPinnedCreateAndDeployInstructions()
        {
            // Arrange
            const ulong lamports = 123;
            const ulong maximumProgramLength = 456;
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(1),
                Pk(2),
                lamports,
                UpgradeableBpfLoaderState.ProgramMetadataLength,
                UpgradeableBpfLoaderProgram.ProgramId);
            var expectedDeploy = UpgradeableBpfLoaderProgram.DeployInstruction(
                Pk(1), Pk(2), Pk(3), Pk(4), maximumProgramLength, closeBuffer: false);

            // Act
            var instructions = UpgradeableBpfLoaderProgram.DeployWithMaximumProgramLength(
                Pk(1), Pk(2), Pk(3), Pk(4), lamports, maximumProgramLength, closeBuffer: false);

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedDeploy);
        }
    }

    [TestFixture]
    public sealed class SetUpgradeAuthority
    {
        [Test]
        public void NewAuthority_IsReadonlyAndDoesNotSign()
        {
            // Arrange
            var programData = UpgradeableBpfLoaderProgram.GetProgramDataAddress(Pk(1));

            // Act
            var instruction = UpgradeableBpfLoaderProgram.SetUpgradeAuthority(Pk(1), Pk(2), Pk(3));

            // Assert
            instruction.Data.Should().Equal(4, 0, 0, 0);
            Metas(instruction).Should().Equal(
                (programData, false, true),
                (Pk(2), true, false),
                (Pk(3), false, false));
        }

        [Test]
        public void NullAuthority_PermanentlyRevokesUpgradeAuthority()
        {
            // Arrange
            var programData = UpgradeableBpfLoaderProgram.GetProgramDataAddress(Pk(1));

            // Act
            var instruction = UpgradeableBpfLoaderProgram.SetUpgradeAuthority(Pk(1), Pk(2), null);

            // Assert
            Metas(instruction).Should().Equal(
                (programData, false, true),
                (Pk(2), true, false));
        }
    }

    [TestFixture]
    public sealed class SetUpgradeAuthorityChecked
    {
        [Test]
        public void NewAuthority_IsRequiredToSign()
        {
            // Arrange
            var programData = UpgradeableBpfLoaderProgram.GetProgramDataAddress(Pk(1));

            // Act
            var instruction = UpgradeableBpfLoaderProgram.SetUpgradeAuthorityChecked(Pk(1), Pk(2), Pk(3));

            // Assert
            instruction.Data.Should().Equal(7, 0, 0, 0);
            Metas(instruction).Should().Equal(
                (programData, false, true),
                (Pk(2), true, false),
                (Pk(3), true, false));
        }
    }

    [TestFixture]
    public sealed class CloseBuffer
    {
        [Test]
        public void DelegatesToPinnedCloseBufferBranch()
        {
            // Act
            var instruction = UpgradeableBpfLoaderProgram.CloseBuffer(Pk(1), Pk(2), Pk(3));

            // Assert
            instruction.Data.Should().Equal(5, 0, 0, 0, 0);
            Metas(instruction).Should().Equal(
                (Pk(1), false, true),
                (Pk(2), false, true),
                (Pk(3), true, false));
        }
    }
}

public static class UpgradeableBpfLoaderStateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void ProgramData_DecodesFixedMetadataAndTrailingBytes()
        {
            // Arrange
            var data = new byte[UpgradeableBpfLoaderState.ProgramDataMetadataLength + 3];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 3);
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), 0x0102030405060708);
            data[12] = 1;
            Pk(7).CopyTo(data.AsSpan(13));
            data[^3] = 0xaa;
            data[^2] = 0xbb;
            data[^1] = 0xcc;

            // Act
            var state = UpgradeableBpfLoaderState.Parse(data);

            // Assert
            state.Kind.Should().Be(UpgradeableBpfLoaderStateKind.ProgramData);
            state.Slot.Should().Be(0x0102030405060708);
            state.Authority.Should().Be(Pk(7));
            state.ProgramBytes.ToArray().Should().Equal(0xaa, 0xbb, 0xcc);
        }

        [Test]
        public void Program_DecodesProgramDataAddressWithoutUnrelatedFields()
        {
            // Arrange
            var data = new byte[UpgradeableBpfLoaderState.ProgramMetadataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(data, 2);
            Pk(8).CopyTo(data.AsSpan(sizeof(uint)));

            // Act
            var state = UpgradeableBpfLoaderState.Parse(data);

            // Assert
            state.Kind.Should().Be(UpgradeableBpfLoaderStateKind.Program);
            state.ProgramDataAddress.Should().Be(Pk(8));
            state.Authority.Should().BeNull();
            state.Slot.Should().BeNull();
            state.ProgramBytes.ToArray().Should().BeEmpty();
        }
    }
}

public static class LoaderV4ProgramTests
{
    [TestFixture]
    public sealed class CreateBuffer
    {
        [Test]
        public void MatchesPinnedRustComposite()
        {
            // Arrange
            const ulong lamports = 123;
            const uint programLength = 456;
            var expectedCreate = SystemProgram.CreateAccount(
                Pk(1), Pk(2), lamports, 0, LoaderV4Program.ProgramId);
            var expectedResize = LoaderV4Program.SetProgramLength(Pk(2), Pk(3), programLength, Pk(4));

            // Act
            var instructions = LoaderV4Program.CreateBuffer(
                Pk(1), Pk(2), lamports, Pk(3), programLength, Pk(4));

            // Assert
            instructions.Should().HaveCount(2);
            instructions[0].Should().BeEquivalentTo(expectedCreate);
            instructions[1].Should().BeEquivalentTo(expectedResize);
        }
    }

    [TestFixture]
    public sealed class Write
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.Write(Pk(1), Pk(2), 0x12345678, [0xaa, 0xbb, 0xcc]))
                .Should().Be("00000000785634120300000000000000aabbcc");
    }

    [TestFixture]
    public sealed class Copy
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.Copy(Pk(1), Pk(2), Pk(3), 1, 2, 3))
                .Should().Be("01000000010000000200000003000000");
    }

    [TestFixture]
    public sealed class SetProgramLength
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.SetProgramLength(Pk(1), Pk(2), 4, Pk(3)))
                .Should().Be("0200000004000000");
    }

    [TestFixture]
    public sealed class Deploy
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.Deploy(Pk(1), Pk(2))).Should().Be("03000000");
    }

    [TestFixture]
    public sealed class Retract
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.Retract(Pk(1), Pk(2))).Should().Be("04000000");
    }

    [TestFixture]
    public sealed class TransferAuthority
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.TransferAuthority(Pk(1), Pk(2), Pk(3))).Should().Be("05000000");
    }

    [TestFixture]
    public sealed class Finalize
    {
        [Test]
        public void MatchesPinnedBincodeVector() =>
            Hex(LoaderV4Program.Finalize(Pk(1), Pk(2), Pk(3))).Should().Be("06000000");
    }
}

public static class LoaderV4StateTests
{
    [TestFixture]
    public sealed class Parse
    {
        [Test]
        public void DecodesNativeFortyEightByteHeader()
        {
            // Arrange
            var data = new byte[LoaderV4State.MetadataLength + 2];
            BinaryPrimitives.WriteUInt64LittleEndian(data, 123);
            Pk(8).CopyTo(data.AsSpan(8));
            BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(40), (ulong)LoaderV4Status.Deployed);
            data[^2] = 0xaa;
            data[^1] = 0xbb;

            // Act
            var state = LoaderV4State.Parse(data);

            // Assert
            state.Slot.Should().Be(123);
            state.AuthorityOrNextVersion.Should().Be(Pk(8));
            state.Status.Should().Be(LoaderV4Status.Deployed);
            state.ProgramBytes.ToArray().Should().Equal(0xaa, 0xbb);
        }
    }
}

internal static class LoaderProgramTestHelpers
{
    internal static PublicKey Pk(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);

    internal static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(static account => (account.PublicKey, account.IsSigner, account.IsWritable))];
}
