using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// A canonical, subgroup-checked compressed BLS12-381 public key in the 48-byte G1 representation
/// used by the pinned Solana SDK.
/// </summary>
public sealed class BlsPublicKey : IEquatable<BlsPublicKey>
{
    /// <summary>The compressed public-key length.</summary>
    public const int Length = BlsOperations.PublicKeyLength;

    private readonly byte[] _bytes;

    private BlsPublicKey(ReadOnlySpan<byte> bytes)
    {
        _bytes = bytes.ToArray();
    }

    internal ReadOnlySpan<byte> Bytes => _bytes;

    /// <summary>Parses a compressed G1 point and rejects malformed, off-curve, wrong-subgroup, and infinity encodings.</summary>
    /// <param name="compressed">The exact 48-byte compressed point.</param>
    /// <returns>The validated public key.</returns>
    /// <exception cref="ArgumentException">The value is not a canonical non-infinity G1 point.</exception>
    public static BlsPublicKey Parse(ReadOnlySpan<byte> compressed)
    {
        if (!BlsOperations.IsValidPublicKey(compressed))
            throw new ArgumentException("BLS public key must be a canonical non-infinity G1 subgroup point.", nameof(compressed));

        return new BlsPublicKey(compressed);
    }

    /// <summary>Parses the standard base64 text emitted by <see cref="ToString"/>.</summary>
    /// <param name="base64">Exactly 64 ASCII base64 characters encoding a 48-byte compressed public key.</param>
    /// <returns>The validated public key.</returns>
    /// <exception cref="ArgumentException">The value is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException">The text or decoded point is invalid.</exception>
    public static BlsPublicKey Parse(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);
        Span<byte> compressed = stackalloc byte[Length];
        if (!BlsOperations.TryDecodeCanonicalBase64(base64, compressed))
            throw new FormatException("BLS public key is not canonical fixed-length base64.");

        try
        {
            return Parse(compressed);
        }
        catch (ArgumentException exception)
        {
            throw new FormatException("BLS public key is not a valid base64 compressed G1 point.", exception);
        }
    }

    /// <summary>Attempts to parse and fully validate a compressed G1 public key.</summary>
    /// <param name="compressed">The candidate compressed point.</param>
    /// <param name="publicKey">The validated public key on success.</param>
    /// <returns><see langword="true"/> when the point is canonical, in G1, and not infinity.</returns>
    public static bool TryParse(
        ReadOnlySpan<byte> compressed,
        [NotNullWhen(true)] out BlsPublicKey? publicKey)
    {
        if (!BlsOperations.IsValidPublicKey(compressed))
        {
            publicKey = null;
            return false;
        }

        publicKey = new BlsPublicKey(compressed);
        return true;
    }

    /// <summary>Attempts to parse the standard base64 representation.</summary>
    /// <param name="base64">The candidate base64 text, or <see langword="null"/>.</param>
    /// <param name="publicKey">The validated public key on success.</param>
    /// <returns><see langword="true"/> when the text and point are valid.</returns>
    public static bool TryParse(
        string? base64,
        [NotNullWhen(true)] out BlsPublicKey? publicKey)
    {
        try
        {
            publicKey = string.IsNullOrWhiteSpace(base64) ? null : Parse(base64);
            return publicKey is not null;
        }
        catch (FormatException)
        {
            publicKey = null;
            return false;
        }
    }

    /// <summary>Returns a new array containing the compressed 48-byte public key.</summary>
    /// <returns>A defensive copy of the compressed public key.</returns>
    public byte[] ToBytes() => [.. _bytes];

    /// <summary>
    /// Verifies a proof of possession using the pinned POP DST and the Solana binding
    /// <c>payload || compressed_public_key</c>.
    /// </summary>
    /// <param name="proof">The validated compressed G2 proof.</param>
    /// <param name="payload">The application payload bound before the public key.</param>
    /// <returns><see langword="true"/> only for a valid proof by this key.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proof"/> is <see langword="null"/>.</exception>
    public bool VerifyProofOfPossession(BlsProofOfPossession proof, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(proof);
        return BlsOperations.VerifyProofOfPossession(_bytes, proof.Bytes, payload);
    }

    /// <summary>
    /// Verifies a proof of possession and returns the typed wrapper required for safe public-key aggregation.
    /// </summary>
    /// <param name="proof">The validated compressed G2 proof.</param>
    /// <param name="payload">The exact application payload used when the proof was created.</param>
    /// <returns>This public key wrapped with verified proof-of-possession provenance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proof"/> is <see langword="null"/>.</exception>
    /// <exception cref="CryptographicException">The proof is invalid for this key and payload.</exception>
    public BlsPopVerifiedPublicKey VerifyAndWrapProofOfPossession(
        BlsProofOfPossession proof,
        ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (!BlsOperations.VerifyProofOfPossession(_bytes, proof.Bytes, payload))
            throw new CryptographicException("The BLS proof of possession is invalid for this public key and payload.");

        return new BlsPopVerifiedPublicKey(this);
    }

    /// <summary>Verifies the vote-program proof bound to <c>ALPENGLOW || vote_account</c>.</summary>
    /// <param name="proof">The validated compressed proof.</param>
    /// <param name="voteAccount">The vote account to which the proof must be bound.</param>
    /// <returns><see langword="true"/> only for the exact vote-account binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="proof"/> is <see langword="null"/>.</exception>
    public bool VerifyVoteProofOfPossession(BlsProofOfPossession proof, PublicKey voteAccount)
    {
        Span<byte> payload = stackalloc byte[BlsKeypair.VoteProofPayloadLength];
        BlsKeypair.WriteVoteProofPayload(voteAccount, payload);
        return VerifyProofOfPossession(proof, payload);
    }

    /// <inheritdoc/>
    public bool Equals(BlsPublicKey? other) => other is not null && _bytes.AsSpan().SequenceEqual(other._bytes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is BlsPublicKey other && Equals(other);

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

    internal static BlsPublicKey FromValidated(ReadOnlySpan<byte> compressed) => new(compressed);
}
