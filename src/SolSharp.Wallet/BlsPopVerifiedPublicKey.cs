namespace SolSharp.Wallet;

/// <summary>
/// A BLS public key whose proof of possession was cryptographically verified before construction.
/// This provenance wrapper is required by the safe public-key aggregation API to prevent rogue-key attacks.
/// </summary>
public sealed class BlsPopVerifiedPublicKey
{
    internal BlsPopVerifiedPublicKey(BlsPublicKey publicKey)
    {
        PublicKey = publicKey;
    }

    /// <summary>The underlying canonical, subgroup-checked public key.</summary>
    public BlsPublicKey PublicKey { get; }

    /// <summary>
    /// Verifies a minimal-public-key-size BLS signature with the exact pinned Solana signature DST.
    /// Signature verification is exposed only after proof-of-possession provenance is established.
    /// </summary>
    /// <param name="signature">The validated compressed G2 signature.</param>
    /// <param name="message">The exact signed message.</param>
    /// <returns><see langword="true"/> only for a valid signature by this key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <see langword="null"/>.</exception>
    public bool Verify(BlsSignature signature, ReadOnlySpan<byte> message)
    {
        ArgumentNullException.ThrowIfNull(signature);
        return BlsOperations.VerifySignature(Bytes, signature.Bytes, message);
    }

    internal ReadOnlySpan<byte> Bytes => PublicKey.Bytes;
}
