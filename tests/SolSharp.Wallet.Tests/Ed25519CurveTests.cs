using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class Ed25519CurveTests
{
    // (hex, expected) from solders' Pubkey.is_on_curve(): on- and off-curve keys plus the canonical
    // edge encodings - all-zero, all-0x01, and all-0xff (a non-canonical y >= p, which exercises the
    // reduction mod p). The v == 0 branch is unreachable: -1/d is a quadratic non-residue mod p, so no
    // field element y satisfies d*y^2 + 1 == 0, and y is always reduced into the field first.
    public static IEnumerable<TestCaseData> Vectors()
    {
        yield return new TestCaseData("c28a70a61c7510a1cd89216ca16cffcaea4987477e86dbccb97046fc2e18384e", true);
        yield return new TestCaseData("d85d8eec7f26e23219072f7955d0f8f66dcd1e54c201c787e892d8f94f61976f", true);
        yield return new TestCaseData("5ad30c5baad27f885137c313f07166ebb39c74720c62cca88e238eb3cca90e3b", true);
        yield return new TestCaseData("390c8c7d7247342cd8100f2f6f770d65d670e58e0351d8ae8e4f6eac342fc231", false);
        yield return new TestCaseData("2210a924798ef86d43f27cf2d0613031dcb5d8d2ef1b321fcead377f6261e547", false);
        yield return new TestCaseData("2792788baba329464d76c44e6d20d4d0a9eed41f69d7c70ac2f403b498c7d670", false);
        yield return new TestCaseData("0000000000000000000000000000000000000000000000000000000000000000", true);
        yield return new TestCaseData("0101010101010101010101010101010101010101010101010101010101010101", true);
        yield return new TestCaseData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", true);
    }

    [TestFixture]
    public sealed class IsOnCurve
    {
        [TestCaseSource(typeof(Ed25519CurveTests), nameof(Vectors))]
        public void MatchesSolanaSdk(string hex, bool expected)
            => Ed25519Curve.IsOnCurve(Convert.FromHexString(hex)).Should().Be(expected);

        [Test]
        public void IgnoresTheSignBit()
        {
            // The high bit of the last byte is the sign of x, not part of y, so flipping it must not
            // change the on-curve result (both encodings return true from solders).
            var key = Convert.FromHexString("c28a70a61c7510a1cd89216ca16cffcaea4987477e86dbccb97046fc2e18384e");
            Ed25519Curve.IsOnCurve(key).Should().BeTrue();

            key[31] |= 0x80;
            Ed25519Curve.IsOnCurve(key).Should().BeTrue();
        }
    }
}
