using System.Numerics;

namespace SolSharp.Wallet;

/// <summary>
/// Minimal Ed25519 field arithmetic for the on-curve test, matching curve25519-dalek's point
/// decompression: the y coordinate is reduced modulo p, and the encoding lies on the curve iff
/// (y^2 - 1) / (d * y^2 + 1) is a square in the field. Public-point math only - no secret material.
/// </summary>
internal static class Ed25519Curve
{
    private static readonly BigInteger P = BigInteger.Pow(2, 255) - 19;
    private static readonly BigInteger YMask = BigInteger.Pow(2, 255) - 1;

    private static readonly BigInteger D =
        BigInteger.Parse("37095705934669439343138083508754565189542113879843219016388785533085940283555");

    private static readonly BigInteger SqrtExponent = (P + 3) / 8;

    public static bool IsOnCurve(ReadOnlySpan<byte> encoded)
    {
        // y is the low 255 bits reduced mod p; the top bit is the sign of x and is ignored here.
        var y = (new BigInteger(encoded, isUnsigned: true, isBigEndian: false) & YMask) % P;

        var y2 = y * y % P;
        var u = Mod(y2 - 1);
        var v = Mod(D * y2 + 1);
        if (v.IsZero)
            return false;

        // x^2 = u / v must be a square. For p = 5 (mod 8) the candidate's square is +/- (u/v),
        // so v * x^2 lands on +/- u exactly when u/v is a square.
        var x = BigInteger.ModPow(u * BigInteger.ModPow(v, P - 2, P) % P, SqrtExponent, P);
        var check = x * x % P * v % P;
        return check == u || check == Mod(-u);
    }

    private static BigInteger Mod(BigInteger value)
    {
        var result = value % P;
        return result.Sign < 0 ? result + P : result;
    }
}
