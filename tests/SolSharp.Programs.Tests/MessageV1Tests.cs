using System.Buffers.Binary;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class MessageV1Tests
{
    private static PublicKey Pk(byte value) => new(Fill(value));

    private static byte[] Fill(byte value) => [.. Enumerable.Repeat(value, PublicKey.Length)];

    // solana-sdk ec7a0467e268774b724d55120ad952b518f27d64
    // message/src/versions/v1/message.rs::byte_layout_without_config, plus VersionedMessage's 0x81 prefix.
    private static byte[] UpstreamWithoutConfig()
    {
        var expected = new List<byte> { MessageV1.VersionPrefix, 1, 0, 0 };
        expected.AddRange(new byte[sizeof(uint)]);
        expected.AddRange(Fill(0xAB));
        expected.Add(1);
        expected.Add(2);
        expected.AddRange(Fill(1));
        expected.AddRange(Fill(2));
        expected.Add(1);
        expected.Add(1);
        expected.AddRange([2, 0]);
        expected.Add(0);
        expected.AddRange([0xDE, 0xAD]);
        return [.. expected];
    }

    // solana-sdk ec7a0467e268774b724d55120ad952b518f27d64
    // message/src/versions/v1/message.rs::byte_layout_with_config, plus VersionedMessage's 0x81 prefix.
    private static byte[] UpstreamWithConfig()
    {
        var expected = new List<byte> { MessageV1.VersionPrefix, 1, 0, 0 };
        expected.AddRange([7, 0, 0, 0]);
        expected.AddRange(Fill(0xBB));
        expected.Add(1);
        expected.Add(2);
        expected.AddRange(Fill(1));
        expected.AddRange(Fill(2));
        expected.AddRange([8, 7, 6, 5, 4, 3, 2, 1]);
        expected.AddRange([0x44, 0x33, 0x22, 0x11]);
        expected.Add(1);
        expected.Add(0);
        expected.AddRange([0, 0]);
        return [.. expected];
    }

    private static byte[] WithHeapSize(uint heapSize)
    {
        var bytes = UpstreamWithoutConfig().ToList();
        bytes[4] = 0b10000;
        var encoded = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(encoded, heapSize);
        bytes.InsertRange(106, encoded);
        return [.. bytes];
    }

    [TestFixture]
    public sealed class Serialize
    {
        [Test]
        public void AllConfigFields_RoundTripInMaskOrder()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(2),
                Accounts = [AccountMeta.WritableSigner(Pk(1))],
                Data = [0xAA]
            };
            var config = new TransactionConfigV1
            {
                PriorityFee = 1_000,
                ComputeUnitLimit = 200_000,
                LoadedAccountsDataSizeLimit = 1_000_000,
                HeapSize = 65_536
            };
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(0xCC)), [instruction], config);

            // Act
            var bytes = message.Serialize();
            var parsed = MessageV1.Deserialize(bytes);

            // Assert
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)).Should().Be(0b11111);
            parsed.Config.Should().Be(config);
            parsed.Serialize().Should().Equal(bytes);
        }

        [Test]
        public void SerializeIntoExactSpan_MatchesAllocatingPath()
        {
            // Arrange
            var message = MessageV1.Deserialize(UpstreamWithoutConfig());
            var destination = new byte[message.GetSerializedLength()];

            // Act
            var written = message.Serialize(destination);

            // Assert
            written.Should().Be(destination.Length);
            destination.Should().Equal(UpstreamWithoutConfig());
        }

        [Test]
        public void SerializeIntoShortSpan_Throws()
        {
            // Arrange
            var message = MessageV1.Deserialize(UpstreamWithoutConfig());

            // Act
            Action act = () => message.Serialize(new byte[message.GetSerializedLength() - 1]);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("destination");
        }
    }

    [TestFixture]
    public sealed class Compile
    {
        [Test]
        public void EmptyConfig_CompilesCanonicalKeyOrderAndHeader()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(2),
                Accounts = [AccountMeta.WritableSigner(Pk(1))],
                Data = [0xDE, 0xAD]
            };

            // Act
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(0xAB)), [instruction]);

            // Assert: unlike the upstream low-level layout fixture, compilation correctly marks the program readonly.
            var expected = UpstreamWithoutConfig();
            expected[3] = 1;
            message.Serialize().Should().Equal(expected);
            message.Config.Should().Be(new TransactionConfigV1());
        }

        [Test]
        public void OrdersAndMergesAccountClassesLikeCompiledKeys()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts =
                [
                    AccountMeta.Readonly(Pk(3)),
                    AccountMeta.ReadonlySigner(Pk(5)),
                    AccountMeta.Writable(Pk(4)),
                    AccountMeta.WritableSigner(Pk(6)),
                    AccountMeta.Readonly(Pk(4))
                ],
                Data = [7]
            };

            // Act
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);
            var decompiled = message.DecompileInstructions();

            // Assert
            message.RequiredSignatures.Should().Be(3);
            message.ReadonlySignedAccounts.Should().Be(1);
            message.ReadonlyUnsignedAccounts.Should().Be(2);
            message.AccountKeys.Should().Equal(Pk(1), Pk(6), Pk(5), Pk(4), Pk(3), Pk(9));
            decompiled.Should().ContainSingle();
            decompiled[0].Accounts.Select(static meta => (meta.PublicKey, meta.IsSigner, meta.IsWritable)).Should().Equal(
                (Pk(3), false, false),
                (Pk(5), true, false),
                (Pk(4), false, true),
                (Pk(6), true, true),
                (Pk(4), false, true));
        }

        [Test]
        public void StringAndTypedLifetimeSpecifierOverloadsMatch()
        {
            // Arrange
            var hash = new Hash(Fill(8));
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [1] };

            // Act
            var typed = MessageV1.Compile(Pk(1), hash, [instruction]);
            var text = MessageV1.Compile(Pk(1), hash.ToString(), [instruction]);

            // Assert
            typed.Serialize().Should().Equal(text.Serialize());
        }

        [Test]
        public void MutatingSourceData_DoesNotMutateCompiledMessage()
        {
            // Arrange
            var data = new byte[] { 7 };
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = data };
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Act
            data[0] = 99;

            // Assert
            message.Instructions[0].Data.Should().Equal(7);
        }

        [Test]
        public void ThirteenSignatures_Throws()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(250),
                Accounts = [.. Enumerable.Range(2, 12).Select(static value => AccountMeta.ReadonlySigner(Pk((byte)value)))],
                Data = []
            };

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 12 signatures*");
        }

        [Test]
        public void SixtyFiveAddresses_Throws()
        {
            // Arrange: payer + 63 instruction accounts + program = 65 distinct addresses.
            var instruction = new Instruction
            {
                ProgramId = Pk(250),
                Accounts = [.. Enumerable.Range(2, 63).Select(static value => AccountMeta.Readonly(Pk((byte)value)))],
                Data = []
            };

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 64 accounts*");
        }

        [Test]
        public void SixtyFiveInstructions_Throws()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [] };
            var instructions = Enumerable.Repeat(instruction, MessageV1.MaxInstructions + 1).ToArray();

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), instructions);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 64 instructions*");
        }

        [Test]
        public void TwoHundredFiftySixInstructionAccountSlots_Throws()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [.. Enumerable.Repeat(AccountMeta.Readonly(Pk(2)), byte.MaxValue + 1)],
                Data = []
            };

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 255 account slots*");
        }

        [Test]
        public void SixtyFiveThousandFiveHundredThirtySixDataBytes_Throws()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = new byte[ushort.MaxValue + 1] };

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*at most 65535 data bytes*");
        }

        [TestCase(0U)]
        [TestCase(32_767U)]
        [TestCase(32_769U)]
        [TestCase(263_168U)]
        public void InvalidHeapSize_Throws(uint heapSize)
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [] };
            var config = new TransactionConfigV1 { HeapSize = heapSize };

            // Act
            Action act = () => MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction], config);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*1024-byte multiple*");
        }
    }

    [TestFixture]
    public sealed class Validate
    {
        [Test]
        public void CompiledMessage_DoesNotThrow()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Readonly(Pk(2))],
                Data = [7]
            };
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);

            // Act
            var act = message.Validate;

            // Assert
            act.Should().NotThrow();
        }

        [Test]
        public void DuplicateAccountIntroducedAfterCompilation_Throws()
        {
            // Arrange
            var instruction = new Instruction { ProgramId = Pk(9), Accounts = [], Data = [7] };
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);
            var accountKeys = message.AccountKeys.Should().BeOfType<List<PublicKey>>().Subject;
            accountKeys.Add(Pk(1));

            // Act
            var act = message.Validate;

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*duplicate account address*");
        }

        [Test]
        public void OutOfRangeInstructionAccountIndexIntroducedAfterCompilation_Throws()
        {
            // Arrange
            var instruction = new Instruction
            {
                ProgramId = Pk(9),
                Accounts = [AccountMeta.Readonly(Pk(2))],
                Data = [7]
            };
            var message = MessageV1.Compile(Pk(1), new Hash(Fill(8)), [instruction]);
            message.Instructions[0].AccountIndexes[0] = byte.MaxValue;

            // Act
            var act = message.Validate;

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*outside*");
        }
    }

    [TestFixture]
    public sealed class Deserialize
    {
        [Test]
        public void WithoutConfig_RoundTripsPinnedUpstreamLayout()
        {
            // Arrange
            var expected = UpstreamWithoutConfig();

            // Act
            var message = MessageV1.Deserialize(expected);

            // Assert
            message.Serialize().Should().Equal(expected);
            message.GetSerializedLength().Should().Be(expected.Length);
            message.RequiredSignatures.Should().Be(1);
            message.ReadonlySignedAccounts.Should().Be(0);
            message.ReadonlyUnsignedAccounts.Should().Be(0);
            message.LifetimeSpecifier.Should().Be(new Hash(Fill(0xAB)));
            message.AccountKeys.Should().Equal(Pk(1), Pk(2));
            message.Config.Should().Be(new TransactionConfigV1());
            message.Instructions.Should().ContainSingle();
            message.Instructions[0].ProgramIdIndex.Should().Be(1);
            message.Instructions[0].AccountIndexes.Should().Equal(0);
            message.Instructions[0].Data.Should().Equal(Convert.FromHexString("DEAD"));
        }

        [Test]
        public void WithConfig_RoundTripsPinnedUpstreamLayoutAndEndianOrder()
        {
            // Arrange
            var expected = UpstreamWithConfig();

            // Act
            var message = MessageV1.Deserialize(expected);

            // Assert
            message.Serialize().Should().Equal(expected);
            message.Config.PriorityFee.Should().Be(0x0102030405060708UL);
            message.Config.ComputeUnitLimit.Should().Be(0x11223344U);
            message.Config.LoadedAccountsDataSizeLimit.Should().BeNull();
            message.Config.HeapSize.Should().BeNull();
        }

        [TestCase(0b000001U)]
        [TestCase(0b000010U)]
        [TestCase(0b100000U)]
        [TestCase(0x80000000U)]
        public void InvalidConfigMask_Throws(uint mask)
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), mask);

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*config mask*");
        }

        [TestCase(32_767U)]
        [TestCase(32_769U)]
        [TestCase(263_168U)]
        public void InvalidHeapSize_Throws(uint heapSize)
        {
            // Arrange
            var bytes = WithHeapSize(heapSize);

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*heap size*");
        }

        [Test]
        public void WrongVersionPrefix_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[0] = MessageV0.VersionPrefix;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*0x81*");
        }

        [Test]
        public void TrailingByte_Throws()
        {
            // Arrange
            byte[] bytes = [.. UpstreamWithoutConfig(), 0xAA];

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*trailing byte*");
        }

        [TestCase(1, 13)]
        [TestCase(40, 65)]
        [TestCase(41, 65)]
        public void CountAboveV1Maximum_Throws(int offset, byte value)
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[offset] = value;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>();
        }

        [Test]
        public void HeaderWithoutWritableFeePayer_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[2] = 1;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*writable signer*");
        }

        [Test]
        public void HeaderRequiringMoreAddressesThanPresent_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[3] = 2;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*cannot satisfy*");
        }

        [Test]
        public void DuplicateAddress_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes.AsSpan(42, PublicKey.Length).CopyTo(bytes.AsSpan(74, PublicKey.Length));

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*duplicate account address*");
        }

        [TestCase(0)]
        [TestCase(2)]
        public void InvalidProgramIndex_Throws(byte programIndex)
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[106] = programIndex;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*program id*");
        }

        [Test]
        public void InvalidInstructionAccountIndex_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[110] = 2;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*account index 2*");
        }

        [Test]
        public void TruncatedInstructionHeader_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig()[..109];

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*instruction header*");
        }

        [Test]
        public void DeclaredPayloadLongerThanRemaining_Throws()
        {
            // Arrange
            var bytes = UpstreamWithoutConfig();
            bytes[108] = 3;

            // Act
            Action act = () => MessageV1.Deserialize(bytes);

            // Assert
            act.Should().Throw<FormatException>().WithMessage("*instruction payloads*");
        }
    }
}
