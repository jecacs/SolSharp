using System.Buffers.Binary;
using System.Text.Json.Serialization;
using SolSharp.Core.Converters;
using SolSharp.Core.Encoding;

namespace SolSharp.Core.Primitives;

/// <summary>
/// A Solana hash value (32 bytes), used for blockhashes, durable nonces, and message hashes.
/// The type stores the bytes without choosing or running a hashing algorithm.
/// </summary>
[JsonConverter(typeof(HashJsonConverter))]
public readonly struct Hash : IEquatable<Hash>
{
    /// <summary>The length of a Solana hash in bytes (32).</summary>
    public const int Length = 32;

    /// <summary>The longest base58 string that can encode a <see cref="Length"/>-byte hash.</summary>
    public const int MaxBase58Length = 44;

    private readonly ulong _a;
    private readonly ulong _b;
    private readonly ulong _c;
    private readonly ulong _d;
    private readonly string? _base58;

    /// <summary>Creates a hash from its 32 raw bytes.</summary>
    /// <param name="bytes">Exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is not <see cref="Length"/> bytes long.</exception>
    public Hash(ReadOnlySpan<byte> bytes) : this(bytes, null)
    {
    }

    /// <summary>Creates a hash from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded hash; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public Hash(string base58) : this(Decode(base58), base58)
    {
    }

    private Hash(ReadOnlySpan<byte> bytes, string? base58)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"Hash must be {Length} bytes, got {bytes.Length}.", nameof(bytes));

        _a = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        _b = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]);
        _d = BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]);
        _base58 = base58;
    }

    /// <summary>Determines whether two hashes hold the same bytes.</summary>
    /// <param name="left">The left hash.</param>
    /// <param name="right">The right hash.</param>
    /// <returns><c>true</c> if the hashes are equal.</returns>
    public static bool operator ==(Hash left, Hash right) => left.Equals(right);

    /// <summary>Determines whether two hashes hold different bytes.</summary>
    /// <param name="left">The left hash.</param>
    /// <param name="right">The right hash.</param>
    /// <returns><c>true</c> if the hashes are not equal.</returns>
    public static bool operator !=(Hash left, Hash right) => !left.Equals(right);

    /// <summary>Parses a hash from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded hash; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <returns>The parsed hash.</returns>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public static Hash Parse(string base58) => new(base58);

    /// <summary>Tries to parse a hash from its base58 string form, without throwing.</summary>
    /// <param name="base58">The base58-encoded hash, or <c>null</c>.</param>
    /// <param name="hash">The parsed hash on success; <see langword="default"/> otherwise.</param>
    /// <returns><c>true</c> if <paramref name="base58"/> decoded to a valid <see cref="Length"/>-byte hash.</returns>
    public static bool TryParse(string? base58, out Hash hash)
    {
        Span<byte> bytes = stackalloc byte[Length];
        if (base58 is not null && Base58.TryDecode32(base58, bytes))
        {
            hash = new(bytes, base58);
            return true;
        }

        hash = default;
        return false;
    }

    /// <summary>Writes the 32 raw bytes into <paramref name="destination"/>.</summary>
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
    }

    /// <summary>Returns the 32 raw bytes of the hash as a new array.</summary>
    /// <returns>A new <see cref="Length"/>-byte array.</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        CopyTo(bytes);
        return bytes;
    }

    /// <summary>Determines whether this hash equals <paramref name="other"/>.</summary>
    /// <param name="other">The hash to compare with.</param>
    /// <returns><c>true</c> if both values hold the same 32 bytes.</returns>
    public bool Equals(Hash other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Hash other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);

    /// <summary>Returns the base58 string form of the hash.</summary>
    /// <returns>The base58-encoded hash.</returns>
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
        var bytes = new byte[Length];
        if (base58 is null || !Base58.TryDecode32(base58, bytes))
            throw new ArgumentException($"Not a valid base58 string: '{base58}'.", nameof(base58));

        return bytes;
    }
}
