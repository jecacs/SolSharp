using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;

namespace SolSharp.Wallet.Tests;

public static class Ed25519CurveTests
{
    // (hex, expected) from solders' Pubkey.is_on_curve(): on- and off-curve keys plus the canonical
    // edge encodings - all-zero, all-0x01, all-0xff, the u == 0 boundaries, and the order-eight points.
    // The all-0xff vector is a non-canonical y >= p, which exercises reduction mod p. The v == 0 branch
    // is unreachable: -1/d is a quadratic non-residue mod p, so no field element y satisfies
    // d*y^2 + 1 == 0, and y is always reduced into the field first.
    public static IEnumerable<TestCaseData> Vectors()
    {
        yield return new("c28a70a61c7510a1cd89216ca16cffcaea4987477e86dbccb97046fc2e18384e", true);
        yield return new("d85d8eec7f26e23219072f7955d0f8f66dcd1e54c201c787e892d8f94f61976f", true);
        yield return new("5ad30c5baad27f885137c313f07166ebb39c74720c62cca88e238eb3cca90e3b", true);
        yield return new("390c8c7d7247342cd8100f2f6f770d65d670e58e0351d8ae8e4f6eac342fc231", false);
        yield return new("2210a924798ef86d43f27cf2d0613031dcb5d8d2ef1b321fcead377f6261e547", false);
        yield return new("2792788baba329464d76c44e6d20d4d0a9eed41f69d7c70ac2f403b498c7d670", false);
        yield return new("0000000000000000000000000000000000000000000000000000000000000000", true);
        yield return new("0101010101010101010101010101010101010101010101010101010101010101", true);
        yield return new("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", true);
        yield return new("0100000000000000000000000000000000000000000000000000000000000000", true);
        yield return new("ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", true);
        yield return new("0000000000000000000000000000000000000000000000000000000000000080", true);
        yield return new("26e8958fc2b227b045c3f489f2ef98f0d5dfac05d3c63339b13802886d53fc05", true);
        yield return new("26e8958fc2b227b045c3f489f2ef98f0d5dfac05d3c63339b13802886d53fc85", true);
        yield return new("c7176a703d4dd84fba3c0b760d10670f2a2053fa2c39ccc64ec7fd7792ac037a", true);
        yield return new("c7176a703d4dd84fba3c0b760d10670f2a2053fa2c39ccc64ec7fd7792ac03fa", true);
    }

    [TestFixture]
    public sealed class IsOnCurve
    {
        private static readonly BigInteger ReferenceP = (BigInteger.One << 255) - 19;

        private static readonly BigInteger ReferenceYMask = (BigInteger.One << 255) - 1;

        private static readonly BigInteger ReferenceD = BigInteger.Parse(
            "37095705934669439343138083508754565189542113879843219016388785533085940283555");

        [TestCaseSource(typeof(Ed25519CurveTests), nameof(Vectors))]
        public void MatchesSolanaSdk(string hex, bool expected)
            => Ed25519Curve.IsOnCurve(Convert.FromHexString(hex)).Should().Be(expected);

        [Test]
        public void MatchesIndependentOracleOnDeterministicCorpus()
        {
            // Arrange: SHA-256 makes a stable, uniformly distributed corpus without committing hundreds
            // of opaque vectors. The BigInteger oracle uses the Legendre symbol rather than BouncyCastle's
            // square-root implementation, so it detects changes in either the constant or the field path.
            var domain = "SolSharp.Ed25519Curve.v1"u8;
            Span<byte> input = stackalloc byte[domain.Length + sizeof(int)];
            Span<byte> encoded = stackalloc byte[32];
            domain.CopyTo(input);

            // Act & Assert
            for (var i = 0; i < 256; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(input[domain.Length..], i);
                SHA256.HashData(input, encoded);

                Ed25519Curve.IsOnCurve(encoded).Should().Be(
                    ReferenceIsOnCurve(encoded),
                    "sample {0} ({1}) must match the independent field oracle",
                    i,
                    Convert.ToHexString(encoded));
            }
        }

        [Test]
        public void IgnoresTheSignBit()
        {
            // The high bit of the last byte is the sign of x, not part of y, so flipping it must not
            // change the on-curve result (both encodings return true from solders).
            // Arrange
            var key = Convert.FromHexString("c28a70a61c7510a1cd89216ca16cffcaea4987477e86dbccb97046fc2e18384e");

            // Act & Assert
            Ed25519Curve.IsOnCurve(key).Should().BeTrue();

            key[31] |= 0x80;
            Ed25519Curve.IsOnCurve(key).Should().BeTrue();
        }

        // A y at or above p is reduced into the field rather than rejected, matching curve25519-dalek.
        // Asserting that y = p + k answers exactly as y = k pins that reduction without needing an
        // external oracle, and it is the property most at risk from any change to the field arithmetic.
        // The range stops at 19 because p = 2^255 - 19, so p + 19 is the first value that overflows into
        // bit 255 - the sign of x, which is masked off and would make the comparison meaningless.
        [Test]
        public void NonCanonicalYIsReducedModuloP()
        {
            // Arrange
            var p = (BigInteger.One << 255) - 19;

            // Act & Assert
            for (var k = 0; k < 19; k++)
            {
                Ed25519Curve.IsOnCurve(Encode(p + k))
                    .Should().Be(Ed25519Curve.IsOnCurve(Encode(k)), "y = p + {0} must reduce to y = {0}", k);
            }
        }

        // Above the 255-bit boundary the top bit is the sign of x, so it is masked off and y wraps to
        // k - 19 rather than continuing the reduction above.
        [Test]
        public void ValuesAboveTheSignBitBoundaryDropTheTopBit()
        {
            // Arrange
            var p = (BigInteger.One << 255) - 19;

            // Act & Assert
            for (var k = 19; k < 40; k++)
            {
                Ed25519Curve.IsOnCurve(Encode(p + k))
                    .Should().Be(Ed25519Curve.IsOnCurve(Encode(k - 19)), "y = p + {0} masks to y = {1}", k, k - 19);
            }
        }

        private static byte[] Encode(BigInteger value)
        {
            var bytes = new byte[32];
            var raw = value.ToByteArray(isUnsigned: true, isBigEndian: false);
            Array.Copy(raw, bytes, Math.Min(raw.Length, 32));
            return bytes;
        }

        private static bool ReferenceIsOnCurve(ReadOnlySpan<byte> encoded)
        {
            var y = (new BigInteger(encoded, isUnsigned: true, isBigEndian: false) & ReferenceYMask) % ReferenceP;
            var y2 = (y * y) % ReferenceP;
            var u = (y2 - 1 + ReferenceP) % ReferenceP;
            var v = ((ReferenceD * y2) + 1) % ReferenceP;
            if (v.IsZero)
                return false;

            // u/v and u*v differ by the square v^2, so they have the same quadratic character.
            var product = (u * v) % ReferenceP;
            return product.IsZero
                   || BigInteger.ModPow(product, (ReferenceP - 1) / 2, ReferenceP) == BigInteger.One;
        }
    }
}
