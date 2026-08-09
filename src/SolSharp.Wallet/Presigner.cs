using System.Security.Cryptography;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// An <see cref="ISigner"/> backed by a signature produced outside this process. Before returning
/// the signature, it verifies that the signature belongs to <see cref="PublicKey"/> and covers the
/// exact requested message. This matches the Solana SDK presigner contract and prevents a signature
/// collected for one transaction from being attached to another.
/// </summary>
/// <param name="publicKey">The key that produced <paramref name="signature"/>.</param>
/// <param name="signature">The externally produced signature.</param>
public sealed class Presigner(PublicKey publicKey, Signature signature) : ISigner
{
    /// <inheritdoc/>
    public PublicKey PublicKey { get; } = publicKey;

    /// <summary>The externally produced signature.</summary>
    public Signature Signature { get; } = signature;

    /// <summary>
    /// Verifies and returns the externally produced signature for <paramref name="message"/>.
    /// </summary>
    /// <param name="message">The exact message bytes that must have been signed.</param>
    /// <returns>A new array containing the 64-byte signature.</returns>
    /// <exception cref="CryptographicException">
    /// The signature is not valid for <paramref name="message"/> and <see cref="PublicKey"/>.
    /// </exception>
    public byte[] Sign(ReadOnlySpan<byte> message)
    {
        if (!Signature.Verify(PublicKey, message))
            throw new CryptographicException("The external signature does not verify for this public key and message.");

        return Signature.ToBytes();
    }
}
