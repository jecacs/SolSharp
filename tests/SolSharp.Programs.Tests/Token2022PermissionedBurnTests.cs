using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Constants;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs.Tests;

public static class Token2022PermissionedBurnTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, PublicKey.Length).ToArray());

    private static byte[] Pod(byte value, int length) => [.. Enumerable.Repeat(value, length)];

    private static string Hex(Instruction instruction) => Convert.ToHexString(instruction.Data).ToLowerInvariant();

    private static string RepeatedHex(byte value, int length)
        => string.Concat(Enumerable.Repeat(value.ToString("x2"), length));

    private static (PublicKey, bool, bool)[] Metas(Instruction instruction)
        => [.. instruction.Accounts.Select(account => (account.PublicKey, account.IsSigner, account.IsWritable))];

    private static Instruction Build(
        ConfidentialProofLocation equalityProofLocation,
        ConfidentialProofLocation ciphertextValidityProofLocation,
        ConfidentialProofLocation rangeProofLocation,
        IReadOnlyList<PublicKey>? multisigSigners = null)
        => Token2022Program.BurnPermissionedConfidentialTokens(
            Key(1),
            Key(2),
            Key(3),
            Pod(0x11, Token2022Program.DecryptableBalanceLength),
            Pod(0x22, Token2022Program.ElGamalCiphertextLength),
            Pod(0x33, Token2022Program.ElGamalCiphertextLength),
            Key(4),
            equalityProofLocation,
            ciphertextValidityProofLocation,
            rangeProofLocation,
            multisigSigners);

    [TestFixture]
    public sealed class BurnPermissionedConfidentialTokens
    {
        [Test]
        public void MixedProofsMatchPinnedDataAndAccountLayout()
        {
            // Act
            var instruction = Build(
                ConfidentialProofLocation.AtInstructionOffset(-1),
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtInstructionOffset(2));
            var decoded = TokenProgram.DecodeInstructionData(instruction.Data);

            // Assert
            instruction.ProgramId.Should().Be(Token2022Program.ProgramId);
            instruction.Data.Should().HaveCount(169);
            Hex(instruction).Should().Be(
                "2e03" +
                RepeatedHex(0x11, Token2022Program.DecryptableBalanceLength) +
                RepeatedHex(0x22, Token2022Program.ElGamalCiphertextLength) +
                RepeatedHex(0x33, Token2022Program.ElGamalCiphertextLength) +
                "ff0002");
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(5), false, false),
                (Key(3), true, false),
                (Key(4), true, false));
            decoded.Should().NotBeNull();
            decoded!.Name.Should().Be("PermissionedBurnExtension");
            decoded.ExtensionInstructionDiscriminator.Should().Be(3);
        }

        [Test]
        public void MixedProofContextsRemainInProofOrderAndUseOneSysvar()
        {
            // Act
            var instruction = Build(
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtInstructionOffset(-2),
                ConfidentialProofLocation.AtContextState(Key(6)));

            // Assert
            instruction.Data.TakeLast(3).Should().Equal(0, 0xfe, 0);
            instruction.Accounts.Count(account => account.PublicKey == PublicKey.Parse(Sysvars.Instructions)).Should().Be(1);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(5), false, false),
                (Key(6), false, false),
                (Key(3), true, false),
                (Key(4), true, false));
        }

        [Test]
        public void ContextProofsOmitInstructionsSysvar()
        {
            // Act
            var instruction = Build(
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtContextState(Key(6)),
                ConfidentialProofLocation.AtContextState(Key(7)));

            // Assert
            instruction.Data.TakeLast(3).Should().Equal(0, 0, 0);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (Key(5), false, false),
                (Key(6), false, false),
                (Key(7), false, false),
                (Key(3), true, false),
                (Key(4), true, false));
        }

        [Test]
        public void InstructionOffsetBoundsUseOneSysvarAndSignedWireBytes()
        {
            // Act
            var instruction = Build(
                ConfidentialProofLocation.AtInstructionOffset(sbyte.MinValue),
                ConfidentialProofLocation.AtInstructionOffset(1),
                ConfidentialProofLocation.AtInstructionOffset(sbyte.MaxValue));

            // Assert
            instruction.Data.TakeLast(3).Should().Equal(0x80, 0x01, 0x7f);
            instruction.Accounts.Count(account => account.PublicKey == PublicKey.Parse(Sysvars.Instructions)).Should().Be(1);
            Metas(instruction).Should().Equal(
                (Key(1), false, true),
                (Key(2), false, true),
                (PublicKey.Parse(Sysvars.Instructions), false, false),
                (Key(3), true, false),
                (Key(4), true, false));
        }

        [Test]
        public void MultisigAcceptsElevenMembersAndPreservesBothAuthorities()
        {
            // Arrange
            var signers = Enumerable.Range(10, 11).Select(value => Key(checked((byte)value))).ToArray();

            // Act
            var instruction = Build(
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtContextState(Key(6)),
                ConfidentialProofLocation.AtContextState(Key(7)),
                signers);

            // Assert
            instruction.Accounts[5].Should().BeEquivalentTo(AccountMeta.ReadonlySigner(Key(3)));
            instruction.Accounts[6].Should().BeEquivalentTo(AccountMeta.Readonly(Key(4)));
            instruction.Accounts.Skip(7).Should().Equal(signers.Select(AccountMeta.ReadonlySigner));
        }

        [Test]
        public void MultisigRejectsMoreThanElevenMembers()
        {
            // Arrange
            var signers = Enumerable.Range(10, 12).Select(value => Key(checked((byte)value))).ToArray();

            // Act
            Action act = () => _ = Build(
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtContextState(Key(6)),
                ConfidentialProofLocation.AtContextState(Key(7)),
                signers);

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName("multisigSigners");
        }

        [TestCase(35, 64, 64, "newDecryptableAvailableBalance")]
        [TestCase(37, 64, 64, "newDecryptableAvailableBalance")]
        [TestCase(36, 63, 64, "auditorCiphertextLow")]
        [TestCase(36, 65, 64, "auditorCiphertextLow")]
        [TestCase(36, 64, 63, "auditorCiphertextHigh")]
        [TestCase(36, 64, 65, "auditorCiphertextHigh")]
        public void InvalidPodLengthsAreRejected(
            int decryptableBalanceLength,
            int lowCiphertextLength,
            int highCiphertextLength,
            string expectedParameterName)
        {
            // Act
            Action act = () => _ = Token2022Program.BurnPermissionedConfidentialTokens(
                Key(1),
                Key(2),
                Key(3),
                Pod(0x11, decryptableBalanceLength),
                Pod(0x22, lowCiphertextLength),
                Pod(0x33, highCiphertextLength),
                Key(4),
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtContextState(Key(6)),
                ConfidentialProofLocation.AtContextState(Key(7)));

            // Assert
            act.Should().Throw<ArgumentException>().WithParameterName(expectedParameterName);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void NullProofLocationsAreRejected(int nullProofIndex)
        {
            // Arrange
            var proofLocations = new[]
            {
                ConfidentialProofLocation.AtContextState(Key(5)),
                ConfidentialProofLocation.AtContextState(Key(6)),
                ConfidentialProofLocation.AtContextState(Key(7))
            };
            proofLocations[nullProofIndex] = null!;

            // Act
            Action act = () => _ = Build(proofLocations[0], proofLocations[1], proofLocations[2]);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("proofLocations");
        }
    }
}
