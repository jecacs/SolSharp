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
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test1PublicKey)));
        }

        [Test]
        public void Rfc8032Test2_DerivesExpectedPublicKey()
        {
            using var keypair = Keypair.FromSeed(Hex(Test2Seed));
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test2PublicKey)));
        }

        [TestCase(0)]
        [TestCase(31)]
        [TestCase(33)]
        public void WrongLength_Throws(int length)
        {
            Action act = () => _ = Keypair.FromSeed(new byte[length]);
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Sign
    {
        [Test]
        public void Rfc8032Test1_EmptyMessage_MatchesVector()
        {
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Sign([]).Should().Equal(Hex(Test1Signature));
        }

        [Test]
        public void Rfc8032Test2_MatchesVector()
        {
            using var keypair = Keypair.FromSeed(Hex(Test2Seed));
            keypair.Sign(Hex(Test2Message)).Should().Equal(Hex(Test2Signature));
        }

        [Test]
        public void SameMessage_IsDeterministic()
        {
            using var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Sign("solsharp"u8).Should().Equal(keypair.Sign("solsharp"u8));
        }
    }

    [TestFixture]
    public sealed class FromSecretKey
    {
        [Test]
        public void SeedPlusPublicKey_DerivesMatchingPublicKey()
        {
            using var keypair = Keypair.FromSecretKey(Hex(Test1Seed + Test1PublicKey));
            keypair.PublicKey.Should().Be(new PublicKey(Hex(Test1PublicKey)));
        }

        [Test]
        public void SeedPlusPublicKey_SignsWithTheSeed()
        {
            using var keypair = Keypair.FromSecretKey(Hex(Test1Seed + Test1PublicKey));
            keypair.Sign([]).Should().Equal(Hex(Test1Signature));
        }

        [TestCase(63)]
        [TestCase(65)]
        public void WrongLength_Throws(int length)
        {
            Action act = () => _ = Keypair.FromSecretKey(new byte[length]);
            act.Should().Throw<ArgumentException>();
        }

        [Test]
        public void PublicKeyHalfDoesNotMatchSeed_Throws()
        {
            Action act = () => _ = Keypair.FromSecretKey(Hex(Test1Seed + Test2PublicKey));
            act.Should().Throw<ArgumentException>();
        }
    }

    [TestFixture]
    public sealed class Generate
    {
        [Test]
        public void ProducesDistinctKeypairs()
        {
            using var a = Keypair.Generate();
            using var b = Keypair.Generate();
            a.PublicKey.Should().NotBe(b.PublicKey);
        }

        [Test]
        public void SignedMessage_Is64Bytes()
        {
            using var keypair = Keypair.Generate();
            keypair.Sign("hello"u8).Length.Should().Be(64);
        }
    }

    [TestFixture]
    public sealed class Dispose
    {
        [Test]
        public void SignAfterDispose_Throws()
        {
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();

            Action act = () => keypair.Sign("abc"u8);
            act.Should().Throw<ObjectDisposedException>();
        }

        [Test]
        public void CalledTwice_DoesNotThrow()
        {
            var keypair = Keypair.FromSeed(Hex(Test1Seed));
            keypair.Dispose();

            Action act = keypair.Dispose;
            act.Should().NotThrow();
        }
    }
}
