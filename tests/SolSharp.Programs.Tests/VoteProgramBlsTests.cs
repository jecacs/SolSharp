using System.Runtime.InteropServices;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;
using SolSharp.Wallet;
using static SolSharp.Programs.Tests.VoteProgramBlsTestHelpers;

namespace SolSharp.Programs.Tests;

public static class VoteProgramBlsTests
{
    [TestFixture]
    public sealed class Authorize
    {
        [Test]
        public void TypedAuthorization_PreservesExistingWireEncoding()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(1));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);
            var raw = VoteAuthorization.VoterWithBls(keypair.PublicKey.ToBytes(), proof.ToBytes());

            // Act
            var typedInstruction = VoteProgram.Authorize(Key(1), Key(2), Key(3), typed);
            var rawInstruction = VoteProgram.Authorize(Key(1), Key(2), Key(3), raw);

            // Assert
            typedInstruction.Data.Should().Equal(rawInstruction.Data);
        }

        [Test]
        public void DefensiveCredentialCopies_CannotMutateTypedAuthorizationWire()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(1));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);
            var expectedPublicKey = keypair.PublicKey.ToBytes();
            var expectedProof = proof.ToBytes();
            var before = VoteProgram.Authorize(Key(1), Key(2), Key(3), typed);

            // Act
            var publicKeyIsArrayBacked = MemoryMarshal.TryGetArray(
                typed.BlsPublicKey, out var publicKeySegment);
            var proofIsArrayBacked = MemoryMarshal.TryGetArray(
                typed.BlsProofOfPossession, out var proofSegment);
            if (publicKeyIsArrayBacked)
                publicKeySegment.Array![publicKeySegment.Offset] ^= byte.MaxValue;
            if (proofIsArrayBacked)
                proofSegment.Array![proofSegment.Offset] ^= byte.MaxValue;
            var after = VoteProgram.Authorize(Key(1), Key(2), Key(3), typed);

            // Assert
            publicKeyIsArrayBacked.Should().BeTrue();
            proofIsArrayBacked.Should().BeTrue();
            typed.BlsPublicKey.ToArray().Should().Equal(expectedPublicKey);
            typed.BlsProofOfPossession.ToArray().Should().Equal(expectedProof);
            after.Data.Should().Equal(before.Data);
        }

        [Test]
        public void TypedAuthorization_MismatchedVoteAccountOrBlsKeyIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            using var otherKeypair = BlsKeypair.Derive([.. Enumerable.Range(1, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);
            var wrongKey = VoteAuthorization.VoterWithBls(otherKeypair.PublicKey, proof);
            var raw = VoteAuthorization.VoterWithBls(keypair.PublicKey.ToBytes(), proof.ToBytes());

            // Act
            Action voteAccountMismatch = () => _ = VoteProgram.Authorize(Key(1), Key(2), Key(3), typed);
            Action keyMismatch = () => _ = VoteProgram.Authorize(Key(9), Key(2), Key(3), wrongKey);
            Action rawAuthorize = () => _ = VoteProgram.Authorize(Key(1), Key(2), Key(3), raw);

            // Assert
            voteAccountMismatch.Should().Throw<ArgumentException>().WithMessage("*vote account*");
            keyMismatch.Should().Throw<ArgumentException>().WithMessage("*BLS public key*");
            rawAuthorize.Should().NotThrow();
        }
    }

    [TestFixture]
    public sealed class AuthorizeChecked
    {
        [Test]
        public void TypedAuthorization_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);

            // Act
            Action act = () => _ = VoteProgram.AuthorizeChecked(Key(1), Key(2), Key(3), typed);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }

    [TestFixture]
    public sealed class AuthorizeWithSeed
    {
        [Test]
        public void TypedAuthorization_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);

            // Act
            Action act = () => _ = VoteProgram.AuthorizeWithSeed(Key(1), Key(2), Key(4), "seed", Key(3), typed);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }

    [TestFixture]
    public sealed class AuthorizeCheckedWithSeed
    {
        [Test]
        public void TypedAuthorization_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = VoteAuthorization.VoterWithBls(keypair.PublicKey, proof);

            // Act
            Action act = () =>
                _ = VoteProgram.AuthorizeCheckedWithSeed(Key(1), Key(2), Key(4), "seed", Key(3), typed);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }

    [TestFixture]
    public sealed class InitializeAccountV2
    {
        [Test]
        public void TypedInitialize_PreservesExistingWireEncoding()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = new VoteInitializeV2(Key(1), Key(2), keypair.PublicKey, proof, Key(3), 25, 75);
            var raw = new VoteInitializeV2(
                Key(1),
                Key(2),
                keypair.PublicKey.ToBytes(),
                proof.ToBytes(),
                Key(3),
                25,
                75);

            // Act
            var typedInstruction = VoteProgram.InitializeAccountV2(Key(9), typed, Key(4), Key(5));
            var rawInstruction = VoteProgram.InitializeAccountV2(Key(9), raw, Key(4), Key(5));

            // Assert
            typedInstruction.Data.Should().Equal(rawInstruction.Data);
            typed.BlsPublicKey.ToArray().Should().Equal(keypair.PublicKey.ToBytes());
            typed.BlsProofOfPossession.ToArray().Should().Equal(proof.ToBytes());
        }

        [Test]
        public void DefensiveCredentialCopies_CannotMutateTypedInitializeWire()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = new VoteInitializeV2(Key(1), Key(2), keypair.PublicKey, proof, Key(3), 25, 75);
            var expectedPublicKey = keypair.PublicKey.ToBytes();
            var expectedProof = proof.ToBytes();
            var before = VoteProgram.InitializeAccountV2(Key(9), typed, Key(4), Key(5));

            // Act
            var publicKeyIsArrayBacked = MemoryMarshal.TryGetArray(
                typed.BlsPublicKey, out var publicKeySegment);
            var proofIsArrayBacked = MemoryMarshal.TryGetArray(
                typed.BlsProofOfPossession, out var proofSegment);
            if (publicKeyIsArrayBacked)
                publicKeySegment.Array![publicKeySegment.Offset] ^= byte.MaxValue;
            if (proofIsArrayBacked)
                proofSegment.Array![proofSegment.Offset] ^= byte.MaxValue;
            var after = VoteProgram.InitializeAccountV2(Key(9), typed, Key(4), Key(5));

            // Assert
            publicKeyIsArrayBacked.Should().BeTrue();
            proofIsArrayBacked.Should().BeTrue();
            typed.BlsPublicKey.ToArray().Should().Equal(expectedPublicKey);
            typed.BlsProofOfPossession.ToArray().Should().Equal(expectedProof);
            after.Data.Should().Equal(before.Data);
        }

        [Test]
        public void TypedInitialize_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = new VoteInitializeV2(Key(1), Key(2), keypair.PublicKey, proof, Key(3), 25, 75);

            // Act
            Action act = () => _ = VoteProgram.InitializeAccountV2(Key(8), typed, Key(4), Key(5));

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }

    [TestFixture]
    public sealed class CreateAccountV2
    {
        [Test]
        public void TypedInitialize_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = new VoteInitializeV2(Key(1), Key(2), keypair.PublicKey, proof, Key(3), 25, 75);

            // Act
            Action act = () => _ = VoteProgram.CreateAccountV2(Key(6), Key(8), typed, Key(4), Key(5), 1);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }

    [TestFixture]
    public sealed class CreateAccountV2WithSeed
    {
        [Test]
        public void TypedInitialize_MismatchedVoteAccountIsRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive([.. Enumerable.Range(0, 32).Select(static value => (byte)value)]);
            var proof = keypair.CreateVoteProofOfPossession(Key(9));
            var typed = new VoteInitializeV2(Key(1), Key(2), keypair.PublicKey, proof, Key(3), 25, 75);

            // Act
            Action act = () =>
                _ = VoteProgram.CreateAccountV2WithSeed(Key(6), Key(8), Key(7), "seed", typed, Key(4), Key(5), 1);

            // Assert
            act.Should().Throw<ArgumentException>().WithMessage("*vote account*");
        }
    }
}

internal static class VoteProgramBlsTestHelpers
{
    internal static PublicKey Key(byte value) => new([.. Enumerable.Repeat(value, PublicKey.Length)]);
}
