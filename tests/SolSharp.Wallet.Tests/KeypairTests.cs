using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class KeypairTests
{
    // RFC 8032, Section 7.1 - Ed25519 known-answer test vectors.
    private const string Test1Seed = "9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60";
    private const string Test1PublicKey = "d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a";

    private const string Test1Signature =
        "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155" +
        "5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b";

    private const string Test2Seed = "4ccd089b28ff96da9db6c346ec114e0f5b8a319f35aba624da8cf6ed4fb8a6fb";
    private const string Test2PublicKey = "3d4017c3e843895a92b70aa74d1b7ebc9c982ccf2ec4968cc0cd55f12af4660c";
    private const string Test2Message = "72";

    private const string Test2Signature =
        "92a009a9f0d4cab8720e820b5f642540a2b27b5416503f8fb3762223ebdb69da" +
        "085ac1e43e15996e458f3613d0f11d8c387b2eaeb4302aeeb00d291612bb0c00";

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    [TestFixture]
    public sealed class FromSeed
    {
        [Test]
        public void Rfc8032Test1_DerivesExpectedPublicKey()
        {
            // Act
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));

            // Assert
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test1PublicKey)));
        }

        [Test]
        public void Rfc8032Test2_DerivesExpectedPublicKey()
        {
            // Act
            using var keypair = Keypair.FromSeed(Hex(Test2Seed));

            // Assert
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test2PublicKey)));
        }

        [TestCase(0)]
        [TestCase(31)]
        [TestCase(33)]
        public void WrongLength_Throws(int length)
        {
            // Act
            Action act = () => _ = Keypair.FromSeed(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void Rfc8032Test1_EmptyMessage_MatchesVector()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));

            // Act & Assert
            keypair.Sign([]).Should().Equal(Hex(Test1Signature));
        }

        [Test]
        public void Rfc8032Test2_MatchesVector()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Hex(Test2Seed));

            // Act & Assert
            keypair.Sign(Hex(Test2Message)).Should().Equal(Hex(Test2Signature));
        }

        [Test]
        public void SameMessage_IsDeterministic()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));

            // Act & Assert
            keypair.Sign("solsharp"u8).Should().Equal(keypair.Sign("solsharp"u8));
        }
    }

    [TestFixture]
    public sealed class SignSignature
    {
        [Test]
        public void Rfc8032Test1_EmptyMessage_MatchesVector()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));

            // Act & Assert
            keypair.SignSignature([]).ToBytes().Should().Equal(Hex(Test1Signature));
        }

        [Test]
        public void AfterDispose_Throws()
        {
            // Arrange
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();

            // Act
            Action act = () => keypair.SignSignature([]);

            // Assert
            act.Should().Throw<ObjectDisposedException>();
        }
    }

    [TestFixture]
    public sealed class FromSecretKey
    {
        [Test]
        public void SeedPlusPublicKey_DerivesMatchingPublicKey()
        {
            // Act
            using var keypair = Keypair.FromSecretKey(Hex(Test1Seed + Test1PublicKey));

            // Assert
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test1PublicKey)));
        }

        [Test]
        public void SeedPlusPublicKey_SignsWithTheSeed()
        {
            // Act
            using var keypair = Keypair.FromSecretKey(Hex(Test1Seed + Test1PublicKey));

            // Assert
            keypair.Sign([]).Should().Equal(Hex(Test1Signature));
        }

        [TestCase(63)]
        [TestCase(65)]
        public void WrongLength_Throws(int length)
        {
            // Act
            Action act = () => _ = Keypair.FromSecretKey(new byte[length]);

            // Assert
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void PublicKeyHalfDoesNotMatchSeed_Throws()
        {
            // Act
            Action act = () => _ = Keypair.FromSecretKey(Hex(Test1Seed + Test2PublicKey));

            // Assert
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Generate
    {
        [Test]
        public void ProducesDistinctKeypairs()
        {
            // Act
            using var a = Keypair.Generate();
            using var b = Keypair.Generate();

            // Assert
            a.PublicKey.Should().NotBe(b.PublicKey);
        }

        [Test]
        public void SignedMessage_Is64Bytes()
        {
            // Arrange
            using var keypair = Keypair.Generate();

            // Act & Assert
            keypair.Sign("hello"u8).Length.Should().Be(64);
        }
    }

    [TestFixture]
    public sealed class Export
    {
        [Test]
        public void BytesSeedBase58AndJson_RoundTripExactUpstreamLayout()
        {
            // Arrange
            var expected = Hex(Test1Seed + Test1PublicKey);
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));

            // Act
            var bytes = keypair.ToBytes();
            var seed = keypair.ToSeedBytes();
            var base58 = keypair.ToBase58String();
            var json = keypair.ToJsonArray();
            byte[]? fromBase58Bytes = null;
            byte[]? fromJsonBytes = null;

            try
            {
                // Assert
                bytes.Should().Equal(expected);
                seed.Should().Equal(Hex(Test1Seed));
                using var fromBase58 = Keypair.FromBase58String(base58);
                using var fromJson = Keypair.FromJsonArray(json);
                fromBase58Bytes = fromBase58.ToBytes();
                fromJsonBytes = fromJson.ToBytes();
                fromBase58Bytes.Should().Equal(expected);
                fromJsonBytes.Should().Equal(expected);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(expected);
                CryptographicOperations.ZeroMemory(bytes);
                CryptographicOperations.ZeroMemory(seed);
                if (fromBase58Bytes is not null)
                    CryptographicOperations.ZeroMemory(fromBase58Bytes);
                if (fromJsonBytes is not null)
                    CryptographicOperations.ZeroMemory(fromJsonBytes);
            }
        }

        [Test]
        public void ReturnedArraysAreIndependentCopies()
        {
            // Arrange
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));
            var exported = keypair.ToBytes();
            var seed = keypair.ToSeedBytes();
            byte[]? afterMutation = null;

            // Act
            exported[0] ^= byte.MaxValue;
            seed[0] ^= byte.MaxValue;

            try
            {
                // Assert
                afterMutation = keypair.ToBytes();
                afterMutation.Should().Equal(Hex(Test1Seed + Test1PublicKey));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(exported);
                CryptographicOperations.ZeroMemory(seed);
                if (afterMutation is not null)
                    CryptographicOperations.ZeroMemory(afterMutation);
            }
        }

        [Test]
        public void AfterDispose_ThrowsForEveryExport()
        {
            // Arrange
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();
            var toBytes = keypair.ToBytes;
            var toSeedBytes = keypair.ToSeedBytes;
            var toBase58 = keypair.ToBase58String;
            var toJson = keypair.ToJsonArray;

            // Act & Assert
            toBytes.Should().Throw<ObjectDisposedException>();
            toSeedBytes.Should().Throw<ObjectDisposedException>();
            toBase58.Should().Throw<ObjectDisposedException>();
            toJson.Should().Throw<ObjectDisposedException>();
        }
    }

    [TestFixture]
    public sealed class Dispose
    {
        [Test]
        public async Task RacingWithExports_ReturnsOnlyCoherentKeysOrObjectDisposed()
        {
            // Arrange
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            var expectedPublicKey = keypair.PublicKey;
            var exported = new ConcurrentBag<byte[]>();
            using var ready = new CountdownEvent(8);
            using var start = new ManualResetEventSlim();
            var workers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            {
                exported.Add(keypair.ToBytes());
                ready.Signal();
                start.Wait();
                for (var i = 0; i < 128; i++)
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
                    using var imported = Keypair.FromSecretKey(bytes);
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
        public void SignAfterDispose_Throws()
        {
            // Arrange
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();

            // Act
            Action act = () => keypair.Sign("abc"u8);

            // Assert
            act.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void CalledTwice_DoesNotThrow()
        {
            // Arrange
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();

            // Act
            var act = keypair.Dispose;

            // Assert
            act.Should().NotThrow();
        }
    }

    [TestFixture]
    public sealed class Finalizer
    {
        [Test]
        public void OnUndisposedKeypair_FinalizesWithoutThrowing()
        {
            // A keypair the caller forgot to dispose must still finalize cleanly (the finalizer
            // zeroes the seed). A throwing finalizer would crash the host, so the keypair being
            // collected is the check.
            // Arrange
            var weak = CreateAbandonedKeypair();

            // Act
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            weak.IsAlive.Should().BeFalse();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference CreateAbandonedKeypair() => new(Keypair.FromSeed(Hex(Test1Seed)));
    }
}
