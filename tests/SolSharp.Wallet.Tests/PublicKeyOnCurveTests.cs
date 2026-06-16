using FluentAssertions;
using NUnit.Framework;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet.Tests;

public static class PublicKeyOnCurveTests
{
    // (hex, expected) pairs from solders' Pubkey.is_on_curve(): random values, edge encodings
    // (all-zero, all-0x01, all-0xff = non-canonical y >= p), and a real public key.
    public static IEnumerable<TestCaseData> Vectors()
    {
        yield return new TestCaseData("390c8c7d7247342cd8100f2f6f770d65d670e58e0351d8ae8e4f6eac342fc231", false);
        yield return new TestCaseData("b7b08716eb3fc12896b96223177494287733c28ee8ba53bdb56b8824577d53ec", false);
        yield return new TestCaseData("c28a70a61c7510a1cd89216ca16cffcaea4987477e86dbccb97046fc2e18384e", true);
        yield return new TestCaseData("51d820c5c3ef80053a88ae3996de50e801865b3698654ebf5200a5fa0939b99d", false);
        yield return new TestCaseData("7a1d7b282bf8234041f35487d86c669fccbfe0e73d7e7320ad0a757003241e75", true);
        yield return new TestCaseData("2210a924798ef86d43f27cf2d0613031dcb5d8d2ef1b321fcead377f6261e547", false);
        yield return new TestCaseData("d85d8eec7f26e23219072f7955d0f8f66dcd1e54c201c787e892d8f94f61976f", true);
        yield return new TestCaseData("1d1fa01d19f4501d295f232278ce3d7e1429d6a18568a07a87ca4399eaa12504", false);
        yield return new TestCaseData("ea33256d8743b2237dbd9150e09a04993544873b364f8b906baf6887fa801a2f", false);
        yield return new TestCaseData("d88d1601aa428652e2da0439264c12bd4bdc41159dba14b76b7f34b5d04f7953", true);
        yield return new TestCaseData("5ad30c5baad27f885137c313f07166ebb39c74720c62cca88e238eb3cca90e3b", true);
        yield return new TestCaseData("855b871337deb0a0df3bc5618216df0064badc23a9a03f999ed1a7ce974162d7", true);
        yield return new TestCaseData("c2599acf009b926bdca4eee2e26df2562b91ab2f789e73654b0c177df325e9d4", true);
        yield return new TestCaseData("63c4fdcc7c4b0236d9705aed197f3ee944eda2e2dae451f3e6847e8df87a8ce1", true);
        yield return new TestCaseData("2792788baba329464d76c44e6d20d4d0a9eed41f69d7c70ac2f403b498c7d670", false);
        yield return new TestCaseData("f9708bdff80ec7accf54ef410dc90d2adb45ec5d1985c2a76ce8a7acc28ed781", false);
        yield return new TestCaseData("0000000000000000000000000000000000000000000000000000000000000000", true);
        yield return new TestCaseData("0101010101010101010101010101010101010101010101010101010101010101", true);
        yield return new TestCaseData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", true);
        yield return new TestCaseData("ea4a6c63e29c520abef5507b132ec5f9954776aebebe7b92421eea691446d22c", true);
    }

    [TestFixture]
    public sealed class IsOnCurve
    {
        [TestCaseSource(typeof(PublicKeyOnCurveTests), nameof(Vectors))]
        public void MatchesSolanaSdk(string hex, bool expected)
            => new PublicKey(Convert.FromHexString(hex)).IsOnCurve().Should().Be(expected);
    }
}
