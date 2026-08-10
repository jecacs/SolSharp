using System.Diagnostics.CodeAnalysis;

namespace SolSharp.Wallet;

/// <summary>
/// A canonical, subgroup-checked 96-byte G2 proof of possession using the pinned Solana POP scheme.
/// </summary>
public sealed class BlsProofOfPossession : IEquatable<BlsProofOfPossession>
{
    /// <summary>The compressed proof length.</summary>
    public const int Length = BlsOperations.SignatureLength;

    private readonly byte[] _bytes;

    private BlsProofOfPossession(ReadOnlySpan<byte> bytes)
    {
        _bytes = [.. bytes];
    }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>Parses a compressed G2 proof and performs canonical, curve, subgroup, and infinity checks.</summary>
    /// <param name="compressed">The exact 96-byte compressed point.</param>
    /// <returns>The validated proof.</returns>
    /// <exception cref="ArgumentException">The value is not a canonical non-infinity G2 point.</exception>
    public static BlsProofOfPossession Parse(ReadOnlySpan<byte> compressed)
    {
        if (!BlsOperations.IsValidSignature(compressed))
            throw new ArgumentException("BLS proof must be a canonical non-infinity G2 subgroup point.", nameof(compressed));

        return new(compressed);
    }

    /// <summary>Parses the standard base64 text emitted by <see cref="ToString"/>.</summary>
    /// <param name="base64">Exactly 128 ASCII base64 characters encoding a 96-byte compressed proof.</param>
    /// <returns>The validated proof.</returns>
    /// <exception cref="ArgumentException">The value is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">The text or decoded point is invalid.</exception>
    public static BlsProofOfPossession Parse(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        Span<byte> compressed = stackalloc byte[Length];
        if (!BlsOperations.TryDecodeCanonicalBase64(base64, compressed))
            throw new FormatException("BLS proof is not canonical fixed-length base64.");

        try
        {
            return Parse(compressed);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("BLS proof is not a valid base64 compressed G2 point.", exception);
        }
    }

    /// <summary>Attempts to parse and fully validate a compressed G2 proof.</summary>
    /// <param name="compressed">The candidate compressed point.</param>
    /// <param name="proof">The validated proof on success.</param>
    /// <returns><see langword="true"/> when the point is canonical, in G2, and not infinity.</returns>
    public static bool TryParse(
        ReadOnlySpan<byte> compressed,
        [NotNullWhen(true)] out BlsProofOfPossession? proof)
    {
        if (!BlsOperations.IsValidSignature(compressed))
        {
            proof = null;
            return false;
        }

        proof = new(compressed);
        return true;
    }

    /// <summary>Attempts to parse the standard base64 representation.</summary>
    /// <param name="base64">The candidate base64 text, or <see langword="null"/>.</param>
    /// <param name="proof">The validated proof on success.</param>
    /// <returns><see langword="true"/> when the text and point are valid.</returns>
    public static bool TryParse(
        string? base64,
        [NotNullWhen(true)] out BlsProofOfPossession? proof)
    {
        try
        {
            proof = string.IsNullOrWhiteSpace(base64) ? null : Parse(base64);
            return proof is not null;
        }
        catch (FormatException)
        {
            proof = null;
            return false;
        }
    }

    /// <summary>Returns a new array containing the compressed 96-byte proof.</summary>
    /// <returns>A defensive copy of the compressed proof.</returns>
    public byte[] ToBytes() => [.. _bytes];

    /// <inheritdoc/>
    public bool Equals(BlsProofOfPossession? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BlsProofOfPossession other && Equals(other);

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

    internal static BlsProofOfPossession FromValidated(ReadOnlySpan<byte> compressed) => new(compressed);
}
