using Org.BouncyCastle.Math.EC.Rfc8032;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// Ed25519 operations on <see cref="PublicKey"/>. These live in Wallet so that Core keeps the key
/// type without depending on a crypto engine.
/// </summary>
public static class PublicKeyExtensions
{
    /// <summary>Verifies an Ed25519 signature of <paramref name="message"/> against this public key.</summary>
    /// <param name="key">The public key the signature must verify under.</param>
    /// <param name="message">The signed message bytes.</param>
    /// <param name="signature">The 64-byte Ed25519 signature to check.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="signature"/> is a valid Ed25519 signature of <paramref name="message"/>
    /// by <paramref name="key"/>; <c>false</c> otherwise, including when <paramref name="signature"/> is not
    /// 64 bytes long.
    /// </returns>
    public static bool Verify(this PublicKey key, ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != Ed25519.SignatureSize)
            return false;

        return Ed25519.Verify(signature.ToArray(), 0, key.ToBytes(), 0, message.ToArray(), 0, message.Length);
    }

    /// <summary>
    /// Determines whether this key's bytes decode to a point on the Ed25519 curve. Program-derived
    /// addresses are deliberately chosen to be off-curve, so this is how they are distinguished from
    /// ordinary (on-curve) account keys.
    /// </summary>
    /// <param name="key">The key to test.</param>
    /// <returns><c>true</c> if the key is a valid curve point.</returns>
    public static bool IsOnCurve(this PublicKey key)
    {
        Span<byte> encoded = stackalloc byte[PublicKey.Length];
        key.CopyTo(encoded);
        return Ed25519Curve.IsOnCurve(encoded);
    }
}
