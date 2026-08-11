using System.Buffers.Binary;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Wallet;

/// <summary>
/// A 64-byte Ed25519 signature with Solana base58 parsing, formatting, and value equality.
/// Signature verification uses the strict Solana-compatible rules implemented by Wallet.
/// </summary>
public readonly struct Signature : IEquatable<Signature>
{
    /// <summary>The length of an Ed25519 signature in bytes (64).</summary>
    public const int Length = 64;

    /// <summary>The longest base58 string that can encode a <see cref="Length"/>-byte signature.</summary>
    public const int MaxBase58Length = 88;

    private readonly ulong _a;
    private readonly ulong _b;
    private readonly ulong _c;
    private readonly ulong _d;
    private readonly ulong _e;
    private readonly ulong _f;
    private readonly ulong _g;
    private readonly ulong _h;
    private readonly string? _base58;

    /// <summary>Creates a signature value from its 64 raw bytes.</summary>
    /// <param name="bytes">Exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is not <see cref="Length"/> bytes long.</exception>
    public Signature(ReadOnlySpan<byte> bytes) : this(bytes, null)
    {
    }

    /// <summary>Creates a signature value from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded signature; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public Signature(string base58) : this(Decode(base58), base58)
    {
    }

    private Signature(ReadOnlySpan<byte> bytes, string? base58)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"Signature must be {Length} bytes, got {bytes.Length}.", nameof(bytes));

        _a = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        _b = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]);
        _d = BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]);
        _e = BinaryPrimitives.ReadUInt64LittleEndian(bytes[32..]);
        _f = BinaryPrimitives.ReadUInt64LittleEndian(bytes[40..]);
        _g = BinaryPrimitives.ReadUInt64LittleEndian(bytes[48..]);
        _h = BinaryPrimitives.ReadUInt64LittleEndian(bytes[56..]);
        _base58 = base58;
    }

    /// <summary>Determines whether two signatures hold the same bytes.</summary>
    /// <param name="left">The left signature.</param>
    /// <param name="right">The right signature.</param>
    /// <returns><c>true</c> if the signatures are equal.</returns>
    public static bool operator ==(Signature left, Signature right) => left.Equals(right);

    /// <summary>Determines whether two signatures hold different bytes.</summary>
    /// <param name="left">The left signature.</param>
    /// <param name="right">The right signature.</param>
    /// <returns><c>true</c> if the signatures are not equal.</returns>
    public static bool operator !=(Signature left, Signature right) => !left.Equals(right);

    /// <summary>Parses a signature from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded signature; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <returns>The parsed signature.</returns>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public static Signature Parse(string base58) => new(base58);

    /// <summary>Tries to parse a signature from its base58 string form, without throwing.</summary>
    /// <param name="base58">The base58-encoded signature, or <c>null</c>.</param>
    /// <param name="signature">The parsed signature on success; <see langword="default"/> otherwise.</param>
    /// <returns><c>true</c> if <paramref name="base58"/> decoded to a valid <see cref="Length"/>-byte signature.</returns>
    public static bool TryParse(string? base58, out Signature signature)
    {
        if (Base58.TryDecode(base58, MaxBase58Length, out var bytes) && bytes.Length == Length)
        {
            signature = new(bytes, base58);
            return true;
        }

        signature = default;
        return false;
    }

    /// <summary>Writes the 64 raw bytes into <paramref name="destination"/>.</summary>
    /// <param name="destination">The span to write into; must be at least <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is smaller than <see cref="Length"/> bytes.</exception>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Length)
            throw new ArgumentException($"Destination must be at least {Length} bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _a);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], _b);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], _c);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[24..], _d);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[32..], _e);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[40..], _f);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[48..], _g);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[56..], _h);
    }

    /// <summary>Returns the 64 raw bytes of the signature as a new array.</summary>
    /// <returns>A new <see cref="Length"/>-byte array.</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        CopyTo(bytes);
        return bytes;
    }

    /// <summary>Verifies this signature against the exact signed message and public key.</summary>
    /// <param name="publicKey">The signer's Ed25519 public key.</param>
    /// <param name="message">The exact message bytes that were signed.</param>
    /// <returns>
    /// <c>true</c> when this is a valid strict Ed25519 signature of <paramref name="message"/> by
    /// <paramref name="publicKey"/>; <c>false</c> otherwise.
    /// </returns>
    public bool Verify(PublicKey publicKey, ReadOnlySpan<byte> message) => publicKey.Verify(message, this);

    /// <summary>Determines whether this signature equals <paramref name="other"/>.</summary>
    /// <param name="other">The signature to compare with.</param>
    /// <returns><c>true</c> if both values hold the same 64 bytes.</returns>
    public bool Equals(Signature other)
        => _a == other._a
           && _b == other._b
           && _c == other._c
           && _d == other._d
           && _e == other._e
           && _f == other._f
           && _g == other._g
           && _h == other._h;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Signature other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(_a);
        hash.Add(_b);
        hash.Add(_c);
        hash.Add(_d);
        hash.Add(_e);
        hash.Add(_f);
        hash.Add(_g);
        hash.Add(_h);
        return hash.ToHashCode();
    }

    /// <summary>Returns the Solana base58 string form of the signature.</summary>
    /// <returns>The base58-encoded signature.</returns>
    public override string ToString()
    {
        if (_base58 is not null)
            return _base58;

        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        return Base58.Encode(bytes);
    }

    private static byte[] Decode(string base58)
    {
        if (!Base58.TryDecode(base58, MaxBase58Length, out var bytes))
            throw new ArgumentException(
                $"Signature must be valid base58 encoding of exactly {Length} bytes (input length {base58?.Length ?? 0}).",
                nameof(base58));

        return bytes;
    }
}
