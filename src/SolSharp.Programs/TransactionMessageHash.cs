using Org.BouncyCastle.Crypto.Digests;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Computes the domain-separated BLAKE3 hash used by the Solana SDK to identify serialized
/// transaction messages. The same algorithm applies to legacy and versioned message bytes.
/// </summary>
public static class TransactionMessageHash
{
    private const int DigestSizeBits = Hash.Length * 8;

    /// <summary>Computes the Solana message hash for <paramref name="message"/>.</summary>
    /// <param name="message">The compiled transaction message.</param>
    /// <returns>The domain-separated 32-byte BLAKE3 hash.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">The message cannot be serialized.</exception>
    public static Hash Compute(ITransactionMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Compute(message.Serialize());
    }

    /// <summary>Computes the Solana message hash for exact serialized message bytes.</summary>
    /// <param name="serializedMessage">The serialized legacy or versioned message.</param>
    /// <returns>The domain-separated 32-byte BLAKE3 hash.</returns>
    public static Hash Compute(ReadOnlySpan<byte> serializedMessage)
    {
        var digest = new Blake3Digest(DigestSizeBits);
        digest.BlockUpdate("solana-tx-message-v1"u8);
        digest.BlockUpdate(serializedMessage);

        Span<byte> result = stackalloc byte[Hash.Length];
        var written = digest.DoFinal(result);
        if (written != Hash.Length)
            throw new InvalidOperationException($"BLAKE3 produced {written} bytes instead of {Hash.Length}.");

        return new Hash(result);
    }
}
