using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using static SolSharp.Wallet.Tests.BlsAggregationVectors;
using Backend = Nethermind.Crypto.Bls;

namespace SolSharp.Wallet.Tests;

public static class BlsPublicKeyTests
{
    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void RejectsInfinityWrongSubgroupAndNoncanonicalTextBeforeDecode()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var infinity = new byte[BlsPublicKey.Length];
            infinity[0] = 0xc0;
            var base64 = keypair.PublicKey.ToString();

            // Act & Assert
            BlsPublicKey.TryParse(infinity, out _).Should().BeFalse();
            BlsPublicKey.TryParse(Convert.FromHexString(G1WrongSubgroup), out _).Should().BeFalse();
            BlsPublicKey.TryParse(new string('A', 1_000_000), out _).Should().BeFalse();
            BlsPublicKey.TryParse(ReplaceAt(base64, 1, ' '), out _).Should().BeFalse();
            BlsPublicKey.TryParse(ReplaceAt(base64, ^1, '='), out _).Should().BeFalse();
            BlsPublicKey.TryParse(ReplaceAt(base64, 2, '*'), out _).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class VerifyAndWrapProofOfPossession
    {
        [Test]
        public void ValidProofCreatesTypedProvenanceWhileMismatchesThrow()
        {
            // Arrange
            using var first = BlsKeypair.Derive(InputKeyMaterial(0));
            using var second = BlsKeypair.Derive(InputKeyMaterial(1));
            var proof = first.CreateProofOfPossession("registry"u8);
            var firstPublicKey = first.PublicKey;
            var secondPublicKey = second.PublicKey;

            // Act
            var verified = firstPublicKey.VerifyAndWrapProofOfPossession(proof, "registry"u8);
            Action wrongKey = () => _ = secondPublicKey.VerifyAndWrapProofOfPossession(proof, "registry"u8);
            Action wrongPayload = () => _ = firstPublicKey.VerifyAndWrapProofOfPossession(proof, "other"u8);

            // Assert
            verified.PublicKey.Should().Be(firstPublicKey);
            wrongKey.Should().Throw<CryptographicException>();
            wrongPayload.Should().Throw<CryptographicException>();
        }
    }
}

public static class BlsPopVerifiedPublicKeyTests
{
    [TestFixture]
    public sealed class Verify
    {
        [Test]
        public void VerifiedProofWrapperCanVerifySignatures()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var proof = keypair.CreateProofOfPossession("registry"u8);
            var verified = keypair.PublicKey.VerifyAndWrapProofOfPossession(proof, "registry"u8);
            var signature = keypair.Sign("message"u8);

            // Act
            var valid = verified.Verify(signature, "message"u8);
            var wrongMessage = verified.Verify(signature, "other"u8);

            // Assert
            valid.Should().BeTrue();
            wrongMessage.Should().BeFalse();
        }
    }
}

public static class BlsSignatureTests
{
    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void RejectsInfinityWrongSubgroupMalformedAndNoncanonicalTextBeforeDecode()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var signature = keypair.Sign("message"u8);
            var infinity = new byte[BlsSignature.Length];
            infinity[0] = 0xc0;
            var wrongSubgroup = Convert.FromHexString(G2WrongSubgroup);
            var base64 = signature.ToString();

            // Act & Assert
            BlsSignature.TryParse(infinity, out _).Should().BeFalse();
            BlsOperations.GetG2ValidationResult(wrongSubgroup)
                .Should().Be(BlsPointValidationResult.NotInGroup);
            BlsSignature.TryParse(wrongSubgroup, out _).Should().BeFalse();
            BlsOperations.GetG2ValidationResult(new byte[BlsSignature.Length])
                .Should().Be(BlsPointValidationResult.BadEncoding);
            BlsSignature.TryParse(ReplaceAt(base64, 1, '\n'), out _).Should().BeFalse();
            BlsSignature.TryParse(ReplaceAt(base64, ^1, '='), out _).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class Aggregate
    {
        [Test]
        public void PinnedUpstreamVector_MatchesCanonicalCompressedAggregate()
        {
            // Arrange
            BlsSignature[] signatures =
            [
                BlsSignature.Parse(Convert.FromHexString(AggregateSignatureOne)),
                BlsSignature.Parse(Convert.FromHexString(AggregateSignatureTwo)),
                BlsSignature.Parse(Convert.FromHexString(AggregateSignatureThree))
            ];

            // Act
            var aggregate = BlsSignature.Aggregate(signatures);

            // Assert
            Convert.ToHexString(aggregate.ToBytes()).Should().Be(ExpectedAggregateSignature);
        }

        [Test]
        public void EmptyNullEntryAndInfinityResult_AreRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var signature = keypair.Sign("message"u8);
            var negative = BlsSignature.Parse(new Backend.P2(signature.Bytes).Neg().Compress());
            BlsSignature[] nullEntry = [signature, null!];

            // Act
            Action empty = static () => _ = BlsSignature.Aggregate([]);
            Action containsNull = () => _ = BlsSignature.Aggregate(nullEntry);
            Action infinity = () => _ = BlsSignature.Aggregate([signature, negative]);

            // Assert
            empty.Should().Throw<ArgumentException>();
            containsNull.Should().Throw<ArgumentException>();
            infinity.Should().Throw<ArgumentException>().WithMessage("*point at infinity*");
        }

        [Test]
        public void MalformedInfinityAndWrongSubgroupInputs_CannotEnterAggregation()
        {
            // Arrange
            var infinity = new byte[BlsSignature.Length];
            infinity[0] = 0xc0;

            // Act & Assert
            BlsSignature.TryParse([1], out _).Should().BeFalse();
            BlsSignature.TryParse(infinity, out _).Should().BeFalse();
            BlsSignature.TryParse(Convert.FromHexString(G2WrongSubgroup), out _).Should().BeFalse();
        }
    }
}

public static class BlsProofOfPossessionTests
{
    [TestFixture]
    public sealed class TryParse
    {
        [Test]
        public void RejectsInfinityMalformedAndNoncanonicalTextBeforeDecode()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var proof = keypair.CreateProofOfPossession("payload"u8);
            var infinity = new byte[BlsProofOfPossession.Length];
            infinity[0] = 0xc0;

            // Act & Assert
            BlsProofOfPossession.TryParse(infinity, out _).Should().BeFalse();
            BlsProofOfPossession.TryParse("not base64", out _).Should().BeFalse();
            BlsProofOfPossession.TryParse(ReplaceAt(proof.ToString(), 2, '*'), out _).Should().BeFalse();
        }
    }
}

public static class BlsAggregatePublicKeyTests
{
    [TestFixture]
    public sealed class Aggregate
    {
        [Test]
        public void PinnedUpstreamFastAggregateVector_MatchesCanonicalCompressedKey()
        {
            // Arrange: the upstream fixture marks these public keys as PopVerified before aggregation.
            BlsPopVerifiedPublicKey[] publicKeys =
            [
                new(BlsPublicKey.Parse(Convert.FromHexString(FastAggregatePublicKeyOne))),
                new(BlsPublicKey.Parse(Convert.FromHexString(FastAggregatePublicKeyTwo)))
            ];

            // Act
            var aggregate = BlsAggregatePublicKey.Aggregate(publicKeys);
            var repeated = BlsAggregatePublicKey.Aggregate(publicKeys);

            // Assert
            Convert.ToHexString(aggregate.ToBytes()).Should().Be(ExpectedAggregatePublicKey);
            aggregate.Should().Be(repeated);
            aggregate.GetHashCode().Should().Be(repeated.GetHashCode());
            aggregate.ToString().Should().Be(Convert.ToBase64String(aggregate.ToBytes()));
        }

        [Test]
        public void EmptyNullEntryAndInfinityResult_AreRejected()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial(0));
            var publicKey = keypair.PublicKey;
            var negative = BlsPublicKey.Parse(new Backend.P1(publicKey.Bytes).Neg().Compress());
            var verified = new BlsPopVerifiedPublicKey(publicKey);
            var negativeVerified = new BlsPopVerifiedPublicKey(negative);
            BlsPopVerifiedPublicKey[] nullEntry = [verified, null!];

            // Act
            Action empty = static () => _ = BlsAggregatePublicKey.Aggregate([]);
            Action containsNull = () => _ = BlsAggregatePublicKey.Aggregate(nullEntry);
            Action infinity = () => _ = BlsAggregatePublicKey.Aggregate([verified, negativeVerified]);

            // Assert
            empty.Should().Throw<ArgumentException>();
            containsNull.Should().Throw<ArgumentException>();
            infinity.Should().Throw<ArgumentException>().WithMessage("*point at infinity*");
        }

        [Test]
        public void MalformedInfinityAndWrongSubgroupInputs_CannotEnterAggregation()
        {
            // Arrange
            var infinity = new byte[BlsPublicKey.Length];
            infinity[0] = 0xc0;

            // Act & Assert
            BlsPublicKey.TryParse([1], out _).Should().BeFalse();
            BlsPublicKey.TryParse(infinity, out _).Should().BeFalse();
            BlsPublicKey.TryParse(Convert.FromHexString(G1WrongSubgroup), out _).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class Verify
    {
        [Test]
        public void PinnedUpstreamFastAggregateVector_VerifiesSharedMessageOnly()
        {
            // Arrange: the upstream fixture marks these public keys as PopVerified before aggregation.
            BlsPopVerifiedPublicKey[] publicKeys =
            [
                new(BlsPublicKey.Parse(Convert.FromHexString(FastAggregatePublicKeyOne))),
                new(BlsPublicKey.Parse(Convert.FromHexString(FastAggregatePublicKeyTwo)))
            ];
            var aggregate = BlsAggregatePublicKey.Aggregate(publicKeys);
            var signature = BlsSignature.Parse(Convert.FromHexString(FastAggregateSignature));
            var message = Convert.FromHexString(
                "5656565656565656565656565656565656565656565656565656565656565656");

            // Act
            var valid = aggregate.Verify(signature, message);
            var wrongMessage = aggregate.Verify(signature, "wrong"u8);

            // Assert
            valid.Should().BeTrue();
            wrongMessage.Should().BeFalse();
        }

        [Test]
        public void PopVerifiedParticipantsAndDuplicateSigner_MatchUpstreamSemantics()
        {
            // Arrange
            using var first = BlsKeypair.Derive(InputKeyMaterial(0));
            using var second = BlsKeypair.Derive(InputKeyMaterial(1));
            var firstVerified = first.PublicKey.VerifyAndWrapProofOfPossession(
                first.CreateProofOfPossession("registry"u8),
                "registry"u8);
            var secondVerified = second.PublicKey.VerifyAndWrapProofOfPossession(
                second.CreateProofOfPossession("registry"u8),
                "registry"u8);
            var firstSignature = first.Sign("shared"u8);
            var secondSignature = second.Sign("shared"u8);
            var aggregateKey = BlsAggregatePublicKey.Aggregate([firstVerified, secondVerified]);
            var aggregateSignature = BlsSignature.Aggregate([firstSignature, secondSignature]);
            var duplicateKey = BlsAggregatePublicKey.Aggregate([firstVerified, firstVerified]);
            var duplicateSignature = BlsSignature.Aggregate([firstSignature, firstSignature]);

            // Act
            var valid = aggregateKey.Verify(aggregateSignature, "shared"u8);
            var wrongMessage = aggregateKey.Verify(aggregateSignature, "other"u8);
            var duplicateValid = duplicateKey.Verify(duplicateSignature, "shared"u8);
            var missingDuplicateSignature = duplicateKey.Verify(firstSignature, "shared"u8);

            // Assert
            valid.Should().BeTrue();
            wrongMessage.Should().BeFalse();
            duplicateValid.Should().BeTrue();
            missingDuplicateSignature.Should().BeFalse();
        }
    }
}

internal static class BlsAggregationVectors
{
    // Ethereum consensus-spec v0.1.2 vectors executed by pinned solana-bls-signatures 3.4.0.
    internal const string AggregateSignatureOne =
        "91347BCCF740D859038FCDCAF233EECEB2A436BCAAEE9B2AA3BFB70EFE29DFB2677562CCBEA1C8E061FB9971B0753C24" +
        "0622FAB78489CE96768259FC01360346DA5B9F579E5DA0D941E4C6BA18A0E64906082375394F337FA1AF2B7127B0D121";

    internal const string AggregateSignatureTwo =
        "9674E2228034527F4C083206032B020310FACE156D4A4685E2FCAEC2F6F3665AA635D90347B6CE124EB879266B1E801D" +
        "185DE36A0A289B85E9039662634F2EEA1E02E670BC7AB849D006A70B2F93B84597558A05B879C8D445F387A5D5B653DF";

    internal const string AggregateSignatureThree =
        "AE82747DDEEFE4FD64CF9CEDB9B04AE3E8A43420CD255E3C7CD06A8D88B7C7F8638543719981C5D16FA3527C468C25F0" +
        "026704A6951BDE891360C7E8D12DDEE0559004CCDBE6046B55BAE1B257EE97F7CDB955773D7CF29ADF3CCBB9975E4EB9";

    internal const string ExpectedAggregateSignature =
        "9712C3EDD73A209C742B8250759DB12549B3EAF43B5CA61376D9F30E2747DBCF842D8B2AC0901D2A093713E20284A767" +
        "0FCF6954E9AB93DE991BB9B313E664785A075FC285806FA5224C82BDE146561B446CCFC706A64B8579513CFC4FF1D930";

    internal const string FastAggregatePublicKeyOne =
        "A491D1B0ECD9BB917989F0E74F0DEA0422EAC4A873E5E2644F368DFFB9A6E20FD6E10C1B77654D067C0618F6E5A7F79A";

    internal const string FastAggregatePublicKeyTwo =
        "B301803F8B5AC4A1133581FC676DFEDC60D891DD5FA99028805E5EA5B08D3491AF75D0707ADAB3B70C6A6A580217BF81";

    internal const string ExpectedAggregatePublicKey =
        "A10D7B8A1F6B4B3E7048D06478B88C0F2257F0517B12FDFE59E33EC6240C39F9FC7D4F04E8A37C33E64258ED2FA45850";

    internal const string FastAggregateSignature =
        "912C3615F69575407DB9392EB21FEE18FFF797EEB2FBE1816366CA2A08AE574D8824DBFAFB4C9EAA1CF61B63C6F9B699" +
        "11F269B664C42947DD1B53EF1081926C1E82BB2A465F927124B08391A5249036146D6F3F1E17FF5F162F779746D830D1";

    internal const string G1WrongSubgroup =
        "8123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    internal const string G2WrongSubgroup =
        "94CD598E4DA3827FBF72D44ABB50D8B71867B55A453DACF4AACE5CE78222DCB6C2DA6B22A47B24CC7E9A358C88A642C8" +
        "1495F5519F918FEA72747905FD0EA49C264EA0F8FFBE17AAF583B21CD9838D246593B5BF94BDDE84191F68C29936EE28";

    internal static byte[] InputKeyMaterial(int offset) =>
        [.. Enumerable.Range(offset, BlsKeypair.MinimumInputKeyMaterialLength).Select(static value => checked((byte)value))];

    internal static string ReplaceAt(string value, Index index, char replacement)
    {
        var chars = value.ToCharArray();
        chars[index] = replacement;
        return new(chars);
    }
}
