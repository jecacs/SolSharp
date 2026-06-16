using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// Produces Ed25519 signatures for a single Solana account. The transaction builder depends on this
/// abstraction rather than on a concrete private key, so an in-memory <see cref="Keypair"/> - or any
/// other synchronous signer implementation - can be used interchangeably.
/// </summary>
public interface ISigner
{
    /// <summary>The public key whose private counterpart this signer holds.</summary>
    PublicKey PublicKey { get; }

    /// <summary>Signs <paramref name="message"/> with the Ed25519 private key.</summary>
    /// <param name="message">The bytes to sign; for a transaction, the serialized message.</param>
    /// <returns>The 64-byte Ed25519 signature.</returns>
    byte[] Sign(ReadOnlySpan<byte> message);
}
