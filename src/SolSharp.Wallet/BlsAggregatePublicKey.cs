using System.Security.Cryptography;

namespace SolSharp.Wallet;

/// <summary>
/// An aggregate of one or more proof-of-possession-verified BLS public keys for same-message verification.
/// Raw public keys cannot be parsed directly into this type, preserving proof provenance at the API boundary.
/// </summary>
public sealed class BlsAggregatePublicKey : IEquatable<BlsAggregatePublicKey>
{
    private readonly byte[] _bytes;

    private BlsAggregatePublicKey(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    /// <summary>
    /// Aggregates one or more proof-of-possession-verified public keys with native BLS12-381 group addition.
    /// Duplicate keys are included repeatedly, matching the pinned Solana SDK.
    /// </summary>
    /// <param name="publicKeys">The nonempty verified public keys to aggregate.</param>
    /// <returns>The aggregate public key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="publicKeys"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The collection is empty, contains <see langword="null"/>, or aggregates to the point at infinity.
    /// </exception>
    /// <exception cref="CryptographicException">The native BLS backend rejects a validated input.</exception>
    public static BlsAggregatePublicKey Aggregate(IReadOnlyList<BlsPopVerifiedPublicKey> publicKeys)
    {
        ArgumentNullException.ThrowIfNull(publicKeys);
        if (publicKeys.Count == 0)
            throw new ArgumentException("At least one proof-verified BLS public key is required for aggregation.", nameof(publicKeys));
        if (publicKeys.Any(publicKey => publicKey is null))
            throw new ArgumentException("BLS public-key aggregation cannot contain null entries.", nameof(publicKeys));

        var aggregate = BlsOperations.AggregatePublicKeys(publicKeys);
        if (!BlsOperations.IsValidPublicKey(aggregate))
            throw new ArgumentException("BLS public keys must not aggregate to the point at infinity.", nameof(publicKeys));

        return new BlsAggregatePublicKey(aggregate);
    }

    /// <summary>Verifies a signature aggregate over one shared message with the pinned Solana signature DST.</summary>
    /// <param name="signature">The subgroup-checked signature aggregate.</param>
    /// <param name="message">The exact shared message signed by every participant.</param>
    /// <returns><see langword="true"/> only when the aggregate signature is valid for this aggregate key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signature"/> is <see langword="null"/>.</exception>
    public bool Verify(BlsSignature signature, ReadOnlySpan<byte> message)
    {
        ArgumentNullException.ThrowIfNull(signature);
        return BlsOperations.VerifySignature(_bytes, signature.Bytes, message);
    }

    /// <summary>Returns a new array containing the canonical compressed 48-byte aggregate public key.</summary>
    /// <returns>A defensive copy of the compressed aggregate public key.</returns>
    public byte[] ToBytes() => [.. _bytes];

    /// <inheritdoc/>
    public bool Equals(BlsAggregatePublicKey? other) =>
        other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BlsAggregatePublicKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var value in _bytes)
            hash.Add(value);
        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => Convert.ToBase64String(_bytes);
}
