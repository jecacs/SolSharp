using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace SolSharp.Wallet;

/// <summary>
/// A canonical, subgroup-checked compressed BLS12-381 signature in the 96-byte G2 representation
/// used by the pinned Solana SDK.
/// </summary>
public sealed class BlsSignature : IEquatable<BlsSignature>
{
    /// <summary>The compressed signature length.</summary>
    public const int Length = BlsOperations.SignatureLength;

    private readonly byte[] _bytes;

    private BlsSignature(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>Parses a compressed G2 point and rejects malformed, off-curve, wrong-subgroup, and infinity encodings.</summary>
    /// <param name="compressed">The exact 96-byte compressed point.</param>
    /// <returns>The validated signature.</returns>
    /// <exception cref="ArgumentException">The value is not a canonical non-infinity G2 point.</exception>
    public static BlsSignature Parse(ReadOnlySpan<byte> compressed)
    {
        if (!BlsOperations.IsValidSignature(compressed))
            throw new ArgumentException("BLS signature must be a canonical non-infinity G2 subgroup point.", nameof(compressed));

        return new BlsSignature(compressed);
    }

    /// <summary>Parses the standard base64 text emitted by <see cref="ToString"/>.</summary>
    /// <param name="base64">Exactly 128 ASCII base64 characters encoding a 96-byte compressed signature.</param>
    /// <returns>The validated signature.</returns>
    /// <exception cref="ArgumentException">The value is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">The text or decoded point is invalid.</exception>
    public static BlsSignature Parse(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        Span<byte> compressed = stackalloc byte[Length];
        if (!BlsOperations.TryDecodeCanonicalBase64(base64, compressed))
            throw new FormatException("BLS signature is not canonical fixed-length base64.");

        try
        {
            return Parse(compressed);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("BLS signature is not a valid base64 compressed G2 point.", exception);
        }
    }

    /// <summary>Attempts to parse and fully validate a compressed G2 signature.</summary>
    /// <param name="compressed">The candidate compressed point.</param>
    /// <param name="signature">The validated signature on success.</param>
    /// <returns><see langword="true"/> when the point is canonical, in G2, and not infinity.</returns>
    public static bool TryParse(
        ReadOnlySpan<byte> compressed,
        [NotNullWhen(true)] out BlsSignature? signature)
    {
        if (!BlsOperations.IsValidSignature(compressed))
        {
            signature = null;
            return false;
        }

        signature = new BlsSignature(compressed);
        return true;
    }

    /// <summary>Attempts to parse the standard base64 representation.</summary>
    /// <param name="base64">The candidate base64 text, or <see langword="null"/>.</param>
    /// <param name="signature">The validated signature on success.</param>
    /// <returns><see langword="true"/> when the text and point are valid.</returns>
    public static bool TryParse(
        string? base64,
        [NotNullWhen(true)] out BlsSignature? signature)
    {
        try
        {
            signature = string.IsNullOrWhiteSpace(base64) ? null : Parse(base64);
            return signature is not null;
        }
        catch (FormatException)
        {
            signature = null;
            return false;
        }
    }

    /// <summary>
    /// Aggregates one or more subgroup-checked signatures with native BLS12-381 group addition.
    /// Duplicate signatures are included repeatedly, matching the pinned Solana SDK.
    /// </summary>
    /// <param name="signatures">The nonempty signatures to aggregate.</param>
    /// <returns>The canonical compressed aggregate signature.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="signatures"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The collection is empty, contains <see langword="null"/>, or aggregates to the point at infinity.
    /// </exception>
    /// <exception cref="CryptographicException">The native BLS backend rejects a validated input.</exception>
    public static BlsSignature Aggregate(IReadOnlyList<BlsSignature> signatures)
    {
        ArgumentNullException.ThrowIfNull(signatures);
        if (signatures.Count == 0)
            throw new ArgumentException("At least one BLS signature is required for aggregation.", nameof(signatures));
        if (signatures.Any(signature => signature is null))
            throw new ArgumentException("BLS signature aggregation cannot contain null entries.", nameof(signatures));

        var aggregate = BlsOperations.AggregateSignatures(signatures);
        if (!BlsOperations.IsValidSignature(aggregate))
            throw new ArgumentException("BLS signatures must not aggregate to the point at infinity.", nameof(signatures));

        return FromValidated(aggregate);
    }

    /// <summary>Returns a new array containing the compressed 96-byte signature.</summary>
    /// <returns>A defensive copy of the compressed signature.</returns>
    public byte[] ToBytes() => [.. _bytes];

    /// <inheritdoc/>
    public bool Equals(BlsSignature? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BlsSignature other && Equals(other);

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

    internal static BlsSignature FromValidated(ReadOnlySpan<byte> compressed) => new(compressed);
}
