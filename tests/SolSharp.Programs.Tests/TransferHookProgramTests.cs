using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;
using static SolSharp.Programs.Tests.TransferHookTestHelpers;

namespace SolSharp.Programs.Tests;

internal static class TransferHookTestHelpers
{
    internal static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    internal static string Hex(byte[] data) => Convert.ToHexString(data).ToLowerInvariant();

    internal static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];
}

public static class TransferHookProgramTests
{
    [TestFixture]
    public sealed class Execute
    {
        [Test]
        public void MatchesPinnedTransferHookInterface()
        {
            // Act
            var instruction = TransferHookProgram.Execute(Key(9), Key(1), Key(2), Key(3), Key(4), 100);

            // Assert
            Hex(instruction.Data).Should().Be("692565c54bfb661a6400000000000000");
            Metas(instruction).Should().Equal(
                (Key(1), false, false),
                (Key(2), false, false),
                (Key(3), false, false),
                (Key(4), false, false));
        }
    }

    [TestFixture]
    public sealed class InitializeExtraAccountMetaList
    {
        [Test]
        public void MatchesPinnedTransferHookInterface()
        {
            // Arrange
            var fixedMeta = ExtraAccountMeta.FromPublicKey(Key(6), isSigner: false, isWritable: true);

            // Act
            var instruction = TransferHookProgram.InitializeExtraAccountMetaList(
                Key(9), Key(5), Key(2), Key(4), [fixedMeta]);

            // Assert
            Hex(instruction.Data).Should().Be(
                "2b220d31a758ebeb01000000" +
                "00" + "0606060606060606060606060606060606060606060606060606060606060606" + "0001");
            Metas(instruction).Should().Equal(
                (Key(5), false, true),
                (Key(2), false, false),
                (Key(4), true, false),
                (PublicKey.Parse(SolanaProgramIds.SystemProgram), false, false));
        }
    }

    [TestFixture]
    public sealed class UpdateExtraAccountMetaList
    {
        [Test]
        public void MatchesPinnedTransferHookInterface()
        {
            // Arrange
            var fixedMeta = ExtraAccountMeta.FromPublicKey(Key(6), isSigner: false, isWritable: true);

            // Act
            var instruction = TransferHookProgram.UpdateExtraAccountMetaList(
                Key(9), Key(5), Key(2), Key(4), [fixedMeta]);

            // Assert
            Hex(instruction.Data).Should().Be(
                "9d692a926655f1ae01000000" +
                "00" + "0606060606060606060606060606060606060606060606060606060606060606" + "0001");
        }
    }

    [TestFixture]
    public sealed class EncodeExecuteExtraAccountMetaList
    {
        [Test]
        public void MatchesPinnedTlvLayout()
        {
            // Arrange
            var meta = ExtraAccountMeta.FromPublicKey(Key(1), isSigner: true, isWritable: false);

            // Act
            var encoded = TransferHookProgram.EncodeExecuteExtraAccountMetaList([meta]);

            // Assert
            Hex(encoded).Should().Be(
                "692565c54bfb661a2700000001000000" +
                "00" + "0101010101010101010101010101010101010101010101010101010101010101" + "0100");
        }
    }

    [TestFixture]
    public sealed class DecodeExecuteExtraAccountMetaList
    {
        [Test]
        public void PinnedTlvLayout_RoundTrips()
        {
            // Arrange
            var meta = ExtraAccountMeta.FromPublicKey(Key(1), isSigner: true, isWritable: false);
            var encoded = TransferHookProgram.EncodeExecuteExtraAccountMetaList([meta]);

            // Act
            var decoded = TransferHookProgram.DecodeExecuteExtraAccountMetaList(encoded);

            // Assert
            decoded.Should().ContainSingle();
            Hex(decoded![0].Encode()).Should().Be(Hex(meta.Encode()));
        }

        [Test]
        public void TruncatedTlvLayout_IsRejected()
        {
            // Arrange
            var meta = ExtraAccountMeta.FromPublicKey(Key(1), isSigner: true, isWritable: false);
            var encoded = TransferHookProgram.EncodeExecuteExtraAccountMetaList([meta]);

            // Act & Assert
            TransferHookProgram.DecodeExecuteExtraAccountMetaList(encoded.AsSpan()[..^1]).Should().BeNull();
        }
    }

    [TestFixture]
    public sealed class GetExtraAccountMetaListSize
    {
        [Test]
        public void SingleEntry_HasPinnedTlvSize()
            => TransferHookProgram.GetExtraAccountMetaListSize(1).Should().Be(51);
    }

    [TestFixture]
    public sealed class ResolveExecuteExtraAccountMetasAsync
    {
        [Test]
        public async Task ResolvesPinnedOffchainAccountOrderingAndPdas()
        {
            // Arrange
            var hookProgram = Key(9);
            var source = Key(1);
            var mint = Key(2);
            var destination = Key(3);
            var authority = Key(4);
            var staticExtra = Key(6);
            const ulong amount = 100;
            var validation = TransferHookProgram.GetExtraAccountMetasAddress(mint, hookProgram);
            var firstPda = ExtraAccountMeta.FromProgramDerivedAddress(
                [
                    ExtraAccountSeed.FromAccountKey(0),
                    ExtraAccountSeed.FromAccountKey(2),
                    ExtraAccountSeed.FromAccountKey(4)
                ],
                isSigner: false,
                isWritable: true);
            var secondPda = ExtraAccountMeta.FromProgramDerivedAddress(
                [
                    ExtraAccountSeed.FromInstructionData(8, 8),
                    ExtraAccountSeed.FromAccountKey(2),
                    ExtraAccountSeed.FromAccountKey(5),
                    ExtraAccountSeed.FromAccountKey(6)
                ],
                isSigner: false,
                isWritable: true);
            var validationData = TransferHookProgram.EncodeExecuteExtraAccountMetaList(
                [
                    ExtraAccountMeta.FromPublicKey(staticExtra, isSigner: true, isWritable: false),
                    firstPda,
                    secondPda
                ]);
            var expectedFirstPda = ProgramDerivedAddress.FindProgramAddress(
                [source.ToBytes(), destination.ToBytes(), validation.ToBytes()], hookProgram).Address;
            var expectedSecondPda = ProgramDerivedAddress.FindProgramAddress(
                [
                    [100, 0, 0, 0, 0, 0, 0, 0],
                    destination.ToBytes(),
                    staticExtra.ToBytes(),
                    expectedFirstPda.ToBytes()
                ],
                hookProgram).Address;

            ValueTask<ReadOnlyMemory<byte>?> Resolve(PublicKey key, CancellationToken cancellationToken)
                => ValueTask.FromResult<ReadOnlyMemory<byte>?>(key == validation ? validationData : null);

            // Act
            var extras = await TransferHookProgram.ResolveExecuteExtraAccountMetasAsync(
                hookProgram,
                source,
                mint,
                destination,
                authority,
                amount,
                validationData,
                Resolve);

            // Assert
            extras.Select(meta => (meta.PublicKey, meta.IsSigner, meta.IsWritable)).Should().Equal(
                (staticExtra, false, false),
                (expectedFirstPda, false, true),
                (expectedSecondPda, false, true));
        }
    }

    [TestFixture]
    public sealed class AddExtraAccountsForExecuteAsync
    {
        [Test]
        public async Task AppendsExtrasThenHookProgramAndValidationAccount()
        {
            // Arrange
            var hookProgram = Key(9);
            var source = Key(1);
            var mint = Key(2);
            var destination = Key(3);
            var authority = Key(4);
            var validation = TransferHookProgram.GetExtraAccountMetasAddress(mint, hookProgram);
            var validationData = TransferHookProgram.EncodeExecuteExtraAccountMetaList(
                [ExtraAccountMeta.FromPublicKey(Key(6), false, true)]);
            var transfer = TokenProgram.TransferChecked(
                source,
                mint,
                destination,
                authority,
                5,
                2,
                PublicKey.Parse(SolanaProgramIds.Token2022Program));

            ValueTask<ReadOnlyMemory<byte>?> Resolve(PublicKey key, CancellationToken cancellationToken)
                => ValueTask.FromResult<ReadOnlyMemory<byte>?>(key == validation ? validationData : null);

            // Act
            var augmented = await TransferHookProgram.AddExtraAccountsForExecuteAsync(
                transfer,
                hookProgram,
                source,
                mint,
                destination,
                authority,
                5,
                Resolve);

            // Assert
            augmented.Accounts.TakeLast(3).Select(meta => meta.PublicKey).Should().Equal(Key(6), hookProgram, validation);
        }
    }
}

public static class ExtraAccountMetaTests
{
    [TestFixture]
    public sealed class FromPublicKey
    {
        [Test]
        public void MatchesPinnedTlvResolutionLayout()
        {
            // Act
            var meta = ExtraAccountMeta.FromPublicKey(Key(6), isSigner: false, isWritable: true);

            // Assert
            Hex(meta.Encode()).Should().Be(
                "00" + "0606060606060606060606060606060606060606060606060606060606060606" + "0001");
        }
    }

    [TestFixture]
    public sealed class FromProgramDerivedAddress
    {
        [Test]
        public void MatchesPinnedTlvResolutionLayout()
        {
            // Arrange
            var seeds = new[]
            {
                ExtraAccountSeed.Literal("ab"u8),
                ExtraAccountSeed.FromInstructionData(8, 8),
                ExtraAccountSeed.FromAccountKey(2),
                ExtraAccountSeed.FromAccountData(1, 3, 4)
            };

            // Act
            var meta = ExtraAccountMeta.FromProgramDerivedAddress(seeds, isSigner: false, isWritable: true);

            // Assert
            Hex(meta.Encode()).Should().Be(
                "01" + "01026162020808030204010304" + new string('0', 19 * 2) + "0001");
        }
    }

    [TestFixture]
    public sealed class FromInstructionDataPublicKey
    {
        [Test]
        public void MatchesPinnedTlvResolutionLayout()
        {
            // Act
            var meta = ExtraAccountMeta.FromInstructionDataPublicKey(7, false, false);

            // Assert
            Hex(meta.Encode()).Should().Be("02" + "0107" + new string('0', 30 * 2) + "0000");
        }
    }

    [TestFixture]
    public sealed class FromAccountDataPublicKey
    {
        [Test]
        public void MatchesPinnedTlvResolutionLayout()
        {
            // Act
            var meta = ExtraAccountMeta.FromAccountDataPublicKey(4, 9, false, true);

            // Assert
            Hex(meta.Encode()).Should().Be("02" + "020409" + new string('0', 29 * 2) + "0001");
        }
    }

    [TestFixture]
    public sealed class DecodeSeeds
    {
        [Test]
        public void MatchesPinnedTlvResolutionLayout()
        {
            // Arrange
            var meta = ExtraAccountMeta.FromProgramDerivedAddress(
                [
                    ExtraAccountSeed.Literal("ab"u8),
                    ExtraAccountSeed.FromInstructionData(8, 8),
                    ExtraAccountSeed.FromAccountKey(2),
                    ExtraAccountSeed.FromAccountData(1, 3, 4)
                ],
                isSigner: false,
                isWritable: true);

            // Act
            var seeds = meta.DecodeSeeds();

            // Assert
            seeds!.Select(seed => seed.Kind).Should().Equal(
                ExtraAccountSeedKind.Literal,
                ExtraAccountSeedKind.InstructionData,
                ExtraAccountSeedKind.AccountKey,
                ExtraAccountSeedKind.AccountData);
        }
    }
}
