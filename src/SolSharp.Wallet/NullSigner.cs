using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// A placeholder <see cref="ISigner"/> for an absent required signer. It identifies the expected
/// public key and always returns the all-zero signature used by Solana partially signed transactions.
/// </summary>
/// <param name="publicKey">The required signer key whose signature is not yet available.</param>
public sealed class NullSigner(PublicKey publicKey) : ISigner
{
    /// <inheritdoc/>
    public PublicKey PublicKey { get; } = publicKey;

    /// <summary>Returns an all-zero Ed25519 signature placeholder.</summary>
    /// <param name="message">The message is intentionally not inspected.</param>
    /// <returns>A new all-zero 64-byte signature.</returns>
    public byte[] Sign(ReadOnlySpan<byte> message) => new byte[Signature.Length];
}
