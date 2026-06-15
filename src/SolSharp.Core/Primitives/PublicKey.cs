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
    public const int Length = 32;

    private readonly ulong _a;
    private readonly ulong _b;
    private readonly ulong _c;
    private readonly ulong _d;
    private readonly string? _base58;

    public PublicKey(ReadOnlySpan<byte> bytes) : this(bytes, null)
    {
    }

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

    public static PublicKey Parse(string base58) => new(base58);

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
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < Length)
            throw new ArgumentException($"Destination must be at least {Length} bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _a);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], _b);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], _c);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[24..], _d);
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[Length];
        CopyTo(bytes);
        return bytes;
    }

    public bool Equals(PublicKey other) => _a == other._a && _b == other._b && _c == other._c && _d == other._d;

    public override bool Equals(object? obj) => obj is PublicKey other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_a, _b, _c, _d);

    public override string ToString()
    {
        if (_base58 is not null)
            return _base58;

        Span<byte> bytes = stackalloc byte[Length];
        CopyTo(bytes);
        return Base58.Encode(bytes);
    }

    public static bool operator ==(PublicKey left, PublicKey right) => left.Equals(right);

    public static bool operator !=(PublicKey left, PublicKey right) => !left.Equals(right);

    private static byte[] Decode(string base58)
    {
        if (!Base58.TryDecode(base58, out var bytes))
            throw new ArgumentException($"Not a valid base58 string: '{base58}'.", nameof(base58));

        return bytes;
    }
}
