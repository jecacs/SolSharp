using System.Collections.Concurrent;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class BlsKeypairTests
{
    // Pinned solana-bls-signatures 3.4.0 contracts: blst_keygen, minimal-pubkey-size POP ciphersuite,
    // little-endian scalar export, SIG/POP DSTs, and payload || compressed-public-key PoP binding.
    private const string ExpectedSecret =
        "5634DB5DF13CE91CDE735366556FD174B4C111BC064E262BA3B037E3B70D3623";

    private const string ExpectedPublicKey =
        "9112A0386A2340714BA0C6D2DF235377A8679C3899D03E6EF04DBA7A50EF49E5A1DC93105E9374E93ED301B63487E17C";

    private const string ExpectedUncompressedPublicKey =
        "1112A0386A2340714BA0C6D2DF235377A8679C3899D03E6EF04DBA7A50EF49E5A1DC93105E9374E93ED301B63487E17C" +
        "09D2DD6CA41991204A237C372B5008EA3B4DBD87DE217363ACBEAE295706ECE8659D72829CD95B41D1CBE377CA832008";

    private const string ExpectedSignature =
        "92AE0C49166D44E8A35E2EF7A94673F75F1A7BC48D69C8906E9B40A9149FEFCBE2A710E6BE3C1B7E27A35003B2B93FC3" +
        "094AEBBEC838FFFBC1010E0D419C546BF861BA141ECCEC9AE7F4D83C457CFFD0F7E1A68E9B8E1099FED204762CD0CBF7";

    private const string ExpectedVoteProof =
        "94DF8CAD9915EF9E41269D181CD2DB7FE0590D52BBB1C10352CA557B454FE0732F0EF953F1938B7C019B75EB7A7831BA" +
        "02B385EDA726BDDC81B84E4B2D95435F6DD947E5CCCD22F4D311BE7BDCE02B0DC3E5B2B428F14F6AB1421B246BA0A5BF";

    private static byte[] InputKeyMaterial => [.. Enumerable.Range(0, 32).Select(value => (byte)value)];

    private static PublicKey VoteAccount =>
        new(Enumerable.Range(0, 32).Select(value => (byte)(0x80 + value)).ToArray());

    [TestFixture]
    public sealed class Generate
    {
        [Test]
        public void ReturnsIndependentCanonicalKeypairs()
        {
            // Act
            using var first = BlsKeypair.Generate();
            using var second = BlsKeypair.Generate();
            var firstSecret = first.ToSecretKeyBytes();
            var secondSecret = second.ToSecretKeyBytes();

            try
            {
                // Assert
                first.PublicKey.ToBytes().Should().HaveCount(BlsPublicKey.Length);
                second.PublicKey.Should().NotBe(first.PublicKey);
                firstSecret.Should().HaveCount(BlsKeypair.SecretKeyLength);
                secondSecret.Should().NotEqual(firstSecret);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(firstSecret);
                CryptographicOperations.ZeroMemory(secondSecret);
            }
        }
    }

    [TestFixture]
    public sealed class Derive
    {
        [Test]
        public void PinnedRustSdkVector_MatchesSecretAndCompressedPublicKey()
        {
            // Act
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);
            var secret = keypair.ToSecretKeyBytes();

            try
            {
                // Assert
                Convert.ToHexString(secret).Should().Be(ExpectedSecret);
                Convert.ToHexString(keypair.PublicKey.ToBytes()).Should().Be(ExpectedPublicKey);
                BlsPublicKey.Parse(keypair.PublicKey.ToBytes()).Should().Be(keypair.PublicKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }

        [TestCase(0)]
        [TestCase(31)]
        public void InputShorterThanSdkMinimum_IsRejected(int length)
        {
            // Act
            Action act = () => _ = BlsKeypair.Derive(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class FromSecretKey
    {
        [Test]
        public void CanonicalLittleEndianSecret_RoundTripsButZeroAndNoncanonicalScalarsFail()
        {
            // Arrange
            var expected = Convert.FromHexString(ExpectedSecret);

            // Act
            using var imported = BlsKeypair.FromSecretKey(expected);
            Action zero = () => _ = BlsKeypair.FromSecretKey(new byte[BlsKeypair.SecretKeyLength]);
            Action noncanonical = () => _ = BlsKeypair.FromSecretKey(
                Enumerable.Repeat(byte.MaxValue, BlsKeypair.SecretKeyLength).ToArray());

            // Assert
            imported.PublicKey.ToBytes().Should().Equal(Convert.FromHexString(ExpectedPublicKey));
            zero.Should().Throw<ArgumentException>();
            noncanonical.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class FromBytes
    {
        [Test]
        public void RustKeypairBytes_RoundTripWithDerivedPublicValidation()
        {
            // Arrange
            var bytes = Convert.FromHexString(ExpectedSecret + ExpectedUncompressedPublicKey);

            try
            {
                // Act
                using var imported = BlsKeypair.FromBytes(bytes);
                bytes[^1] ^= 1;
                Action mismatched = () => _ = BlsKeypair.FromBytes(bytes);
                var importedBytes = imported.ToBytes();

                try
                {
                    // Assert
                    Convert.ToHexString(importedBytes).Should().Be(ExpectedSecret + ExpectedUncompressedPublicKey);
                    mismatched.Should().Throw<ArgumentException>().WithMessage("*does not match*");
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(importedBytes);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    [TestFixture]
    public sealed class FromJsonArray
    {
        [Test]
        public void StringAndUtf8CompatibilityOverloads_RoundTrip()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);
            var json = keypair.ToJsonArray();
            var utf8Json = keypair.ToJsonUtf8Bytes();

            try
            {
                // Act
                using var fromString = BlsKeypair.FromJsonArray(json);
                using var fromUtf8 = BlsKeypair.FromJsonArray(utf8Json);

                // Assert
                fromString.PublicKey.Should().Be(keypair.PublicKey);
                fromUtf8.PublicKey.Should().Be(keypair.PublicKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(utf8Json);
            }
        }
    }

    [TestFixture]
    public sealed class DeriveFromSigner
    {
        [Test]
        public void NullSignerPlaceholder_IsRejectedBeforeKeyDerivation()
        {
            // Arrange
            var signer = new NullSigner(new PublicKey(new byte[PublicKey.Length]));

            // Act
            Action act = () => _ = BlsKeypair.DeriveFromSigner(signer, "seed"u8);

            // Assert
            act.Should().Throw<CryptographicException>().WithMessage("*all-zero*");
        }

        [Test]
        public void Ed25519SignerAndPublicSeed_DeterministicallyDomainSeparateDerivedKeys()
        {
            // Arrange
            using var signer = Keypair.FromSeed(Enumerable.Repeat((byte)7, Keypair.SeedLength).ToArray());

            // Act
            using var first = BlsKeypair.DeriveFromSigner(signer, "first"u8);
            using var repeat = BlsKeypair.DeriveFromSigner(signer, "first"u8);
            using var second = BlsKeypair.DeriveFromSigner(signer, "second"u8);

            // Assert
            first.PublicKey.Should().Be(repeat.PublicKey);
            first.PublicKey.Should().NotBe(second.PublicKey);
        }
    }

    [TestFixture]
    public sealed class ToSecretKeyBytes
    {
        [Test]
        public void ReturnsCanonicalDefensiveCopy()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var first = keypair.ToSecretKeyBytes();
            var second = keypair.ToSecretKeyBytes();

            try
            {
                // Assert
                Convert.ToHexString(first).Should().Be(ExpectedSecret);
                first[0] ^= byte.MaxValue;
                Convert.ToHexString(second).Should().Be(ExpectedSecret);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(first);
                CryptographicOperations.ZeroMemory(second);
            }
        }
    }

    [TestFixture]
    public sealed class ToBytes
    {
        [Test]
        public void MatchesPinnedRustKeypairRepresentation()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var bytes = keypair.ToBytes();

            try
            {
                // Assert
                Convert.ToHexString(bytes).Should().Be(ExpectedSecret + ExpectedUncompressedPublicKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    [TestFixture]
    public sealed class ToJsonArray
    {
        [Test]
        public void StringCompatibilityExport_RoundTrips()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var json = keypair.ToJsonArray();
            using var imported = BlsKeypair.FromJsonArray(json);

            // Assert
            json.Should().StartWith("[").And.EndWith("]");
            imported.PublicKey.Should().Be(keypair.PublicKey);
        }
    }

    [TestFixture]
    public sealed class ToJsonUtf8Bytes
    {
        [Test]
        public void ZeroableUtf8Export_RoundTrips()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var json = keypair.ToJsonUtf8Bytes();

            try
            {
                using var imported = BlsKeypair.FromJsonArray(json);

                // Assert
                json[0].Should().Be((byte)'[');
                json[^1].Should().Be((byte)']');
                imported.PublicKey.Should().Be(keypair.PublicKey);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
    }

    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void PinnedRustSdkVector_UsesExactSignatureDst()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var signature = keypair.Sign("SolSharp BLS KAT"u8);

            // Assert
            Convert.ToHexString(signature.ToBytes()).Should().Be(ExpectedSignature);
            BlsPublicKey.Parse(keypair.PublicKey.ToString()).Should().Be(keypair.PublicKey);
            BlsSignature.Parse(signature.ToString()).Should().Be(signature);
            BlsPublicKey.TryParse("not base64", out _).Should().BeFalse();
            BlsSignature.TryParse("not base64", out _).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class Verify
    {
        [Test]
        public void DerivedKeypairUsesPopVerifiedSignatureBoundary()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);
            var signature = keypair.Sign("SolSharp BLS KAT"u8);

            // Act
            var valid = keypair.Verify(signature, "SolSharp BLS KAT"u8);
            var wrongMessage = keypair.Verify(signature, "SolSharp BLS kat"u8);

            // Assert
            valid.Should().BeTrue();
            wrongMessage.Should().BeFalse();
            keypair.PopVerifiedPublicKey.PublicKey.Should().Be(keypair.PublicKey);
        }
    }

    [TestFixture]
    public sealed class CreateVoteProofOfPossession
    {
        [Test]
        public void PinnedVoteVector_BindsAlpenglowVoteAccountAndCompressedPublicKey()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);

            // Act
            var proof = keypair.CreateVoteProofOfPossession(VoteAccount);

            // Assert
            Convert.ToHexString(proof.ToBytes()).Should().Be(ExpectedVoteProof);
            keypair.PublicKey.VerifyVoteProofOfPossession(proof, VoteAccount).Should().BeTrue();
            BlsProofOfPossession.Parse(proof.ToString()).Should().Be(proof);
            BlsProofOfPossession.TryParse("not base64", out _).Should().BeFalse();

            var otherVoteAccount = new PublicKey(Enumerable.Repeat((byte)42, PublicKey.Length).ToArray());
            keypair.PublicKey.VerifyVoteProofOfPossession(proof, otherVoteAccount).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class CreateProofOfPossession
    {
        [Test]
        public void CustomPayloadAndPublicKeyAreBothBound()
        {
            // Arrange
            using var keypair = BlsKeypair.Derive(InputKeyMaterial);
            using var otherKeypair = BlsKeypair.Derive(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

            // Act
            var proof = keypair.CreateProofOfPossession("payload"u8);

            // Assert
            keypair.PublicKey.VerifyProofOfPossession(proof, "payload"u8).Should().BeTrue();
            keypair.PublicKey.VerifyProofOfPossession(proof, "other"u8).Should().BeFalse();
            otherKeypair.PublicKey.VerifyProofOfPossession(proof, "payload"u8).Should().BeFalse();
        }
    }

    [TestFixture]
    public sealed class Dispose
    {
        [Test]
        public async Task RacingWithExports_ReturnsOnlyCoherentKeysOrObjectDisposed()
        {
            // Arrange
            var keypair = BlsKeypair.Derive(InputKeyMaterial);
            var expectedPublicKey = keypair.PublicKey;
            var exported = new ConcurrentBag<byte[]>();
            using var ready = new CountdownEvent(4);
            using var start = new ManualResetEventSlim();
            var workers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
            {
                exported.Add(keypair.ToBytes());
                ready.Signal();
                start.Wait();
                for (var i = 0; i < 8; i++)
                {
                    try
                    {
                        exported.Add(keypair.ToBytes());
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                }
            })).ToArray();

            ready.Wait();
            var dispose = Task.Run(() =>
            {
                start.Wait();
                keypair.Dispose();
            });

            // Act
            start.Set();
            await Task.WhenAll(workers.Append(dispose));

            // Assert
            try
            {
                exported.Should().NotBeEmpty();
                foreach (var bytes in exported)
                {
                    using var imported = BlsKeypair.FromBytes(bytes);
                    imported.PublicKey.Should().Be(expectedPublicKey);
                }
            }
            finally
            {
                foreach (var bytes in exported)
                    CryptographicOperations.ZeroMemory(bytes);
                keypair.Dispose();
            }
        }

        [Test]
        public void SecretOperationsThrowAfterDisposeWhilePublicKeyRemainsUsable()
        {
            // Arrange
            var keypair = BlsKeypair.Derive(InputKeyMaterial);
            var publicKey = keypair.PublicKey;
            keypair.Dispose();

            // Act
            Action sign = () => _ = keypair.Sign([]);
            Action proof = () => _ = keypair.CreateProofOfPossession([]);
            Action export = () => _ = keypair.ToSecretKeyBytes();
            Action jsonExport = () => _ = keypair.ToJsonUtf8Bytes();

            // Assert
            sign.Should().Throw<ObjectDisposedException>();
            proof.Should().Throw<ObjectDisposedException>();
            export.Should().Throw<ObjectDisposedException>();
            jsonExport.Should().Throw<ObjectDisposedException>();
            publicKey.ToBytes().Should().HaveCount(BlsPublicKey.Length);
        }
    }
}
