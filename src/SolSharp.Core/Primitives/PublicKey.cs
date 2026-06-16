using System.Buffers.Binary;
using System.Text.Json.Serialization;
using SolSharp.Core.Converters;
using SolSharp.Core.Encoding;

namespace SolSharp.Core.Primitives;

/// <summary>
/// A Solana public key (32 bytes). Value type with value equality, stored inline as four
/// 64-bit words to avoid per-key heap allocations. The base58 form is cached only when the
/// key is built from a string, so constructing from raw bytes stays allocation-free.
/// </summary>
[JsonConverter(typeof(PublicKeyJsonConverter))]
public readonly struct PublicKey : IEquatable<PublicKey>
{
    /// <summary>The length of a Solana public key in bytes (32).</summary>
    public const int Length = 32;

    private readonly ulong _a;
    private readonly ulong _b;
    private readonly ulong _c;
    private readonly ulong _d;
    private readonly string? _base58;

    /// <summary>Creates a public key from its 32 raw bytes.</summary>
    /// <param name="bytes">Exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> is not <see cref="Length"/> bytes long.</exception>
    public PublicKey(ReadOnlySpan<byte> bytes) : this(bytes, null)
    {
    }

    /// <summary>Creates a public key from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded key; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public PublicKey(string base58) : this(Decode(base58), base58)
    {
    }

    private PublicKey(ReadOnlySpan<byte> bytes, string? base58)
    {
        if (bytes.Length != Length)
            throw new ArgumentException($"Public key must be {Length} bytes, got {bytes.Length}.", nameof(bytes));

        _a = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        _b = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]);
        _d = BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]);
        _base58 = base58;
    }

    /// <summary>Parses a public key from its base58 string form.</summary>
    /// <param name="base58">The base58-encoded key; must decode to exactly <see cref="Length"/> bytes.</param>
    /// <returns>The parsed key.</returns>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is not valid base58 or does not decode to <see cref="Length"/> bytes.</exception>
    public static PublicKey Parse(string base58) => new(base58);

    /// <summary>Tries to parse a public key from its base58 string form, without throwing.</summary>
    /// <param name="base58">The base58-encoded key, or <c>null</c>.</param>
    /// <param name="key">The parsed key on success; <see langword="default"/> otherwise.</param>
    /// <returns><c>true</c> if <paramref name="base58"/> decoded to a valid <see cref="Length"/>-byte key.</returns>
    public static bool TryParse(string? base58, out PublicKey key)
    {
        if (Base58.TryDecode(base58, out var bytes) && bytes.Length == Length)
        {
            key = new PublicKey(bytes, base58);
            return true;
        }

        key = default;
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

    /// <summary>Returns the 32 raw bytes of the key as a new array.</summary>
    /// <returns>A new <see cref="Length"/>-byte array.</returns>
    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        CopyTo(bytes);
        return bytes;
    }

    /// <summary>Determines whether this key equals <paramref name="other"/>.</summary>
    /// <param name="other">The key to compare with.</param>
    /// <returns><c>true</c> if both keys hold the same 32 bytes.</returns>
    public bool Equals(PublicKey other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PublicKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);

    /// <summary>Returns the base58 string form of the key.</summary>
    /// <returns>The base58-encoded key.</returns>
    public override string ToString()
    {
        if (_base58 is not null)
            return _base58;

        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        return Base58.Encode(bytes);
    }

    /// <summary>Determines whether two keys hold the same bytes.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><c>true</c> if the keys are equal.</returns>
    public static bool operator ==(PublicKey left, PublicKey right) => left.Equals(right);

    /// <summary>Determines whether two keys hold different bytes.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns><c>true</c> if the keys are not equal.</returns>
    public static bool operator !=(PublicKey left, PublicKey right) => !left.Equals(right);

    private static byte[] Decode(string base58)
    {
        if (!Base58.TryDecode(base58, out var bytes))
            throw new ArgumentException($"Not a valid base58 string: '{base58}'.", nameof(base58));

        return bytes;
    }
}
