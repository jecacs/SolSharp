using Org.BouncyCastle.Math.EC.Rfc7748;

namespace SolSharp.Wallet;

/// <summary>
/// Minimal Ed25519 field arithmetic for the on-curve test, matching curve25519-dalek's point
/// decompression: the y coordinate is reduced modulo p, and the encoding lies on the curve iff
/// (y^2 - 1) / (d * y^2 + 1) is a square in the field. Public-point math only - no secret material.
/// </summary>
internal static class Ed25519Curve
{
    // d = -121665/121666 mod p, little-endian, in the packed field representation.
    private static readonly int[] D = DecodeD();

    public static bool IsOnCurve(ReadOnlySpan<byte> encoded)
    {
        // The top bit is the sign of x and is not part of y. Everything below it is taken as y; values
        // at or above p are reduced by the field arithmetic itself, which is what curve25519-dalek does
        // and why a canonical-encoding check is deliberately absent here.
        Span<byte> y = stackalloc byte[32];
        encoded[..32].CopyTo(y);
        y[31] &= 0x7F;

        var fieldY = X25519Field.Create();
        X25519Field.Decode255(y, fieldY);

        var y2 = X25519Field.Create();
        X25519Field.Sqr(fieldY, y2);

        var u = X25519Field.Create();
        X25519Field.Copy(y2, 0, u, 0);
        X25519Field.SubOne(u);

        var v = X25519Field.Create();
        X25519Field.Mul(y2, D, v);
        X25519Field.AddOne(v);

        // SqrtRatioVar reports whether u/v is a square, which is exactly the on-curve condition, and
        // handles v = 0 by reporting failure. It replaces a modular inverse plus a square-root
        // exponentiation, each of which cost a full 255-bit BigInteger.ModPow.
        var x = X25519Field.Create();
        return X25519Field.SqrtRatioVar(u, v, x);
    }

    private static int[] DecodeD()
    {
        ReadOnlySpan<byte> littleEndian =
        [
            0xA3, 0x78, 0x59, 0x13, 0xCA, 0x4D, 0xEB, 0x75, 0xAB, 0xD8, 0x41, 0x41, 0x4D, 0x0A, 0x70, 0x00,
            0x98, 0xE8, 0x79, 0x77, 0x79, 0x40, 0xC7, 0x8C, 0x73, 0xFE, 0x6F, 0x2B, 0xEE, 0x6C, 0x03, 0x52
        ];

        var d = X25519Field.Create();
        X25519Field.Decode255(littleEndian, d);
        return d;
    }
}
