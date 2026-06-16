namespace SolSharp.Core.Encoding;

/// <summary>
/// compact-u16 ("shortvec"): a value in [0, 65535] encoded in 1-3 bytes, 7 bits per
/// byte, little-endian, high bit = continuation. Solana prefixes every wire-format
/// array (signatures, accounts, instructions) with this length.
/// </summary>
public static class ShortVec
{
    /// <summary>The largest value compact-u16 can represent (65535).</summary>
    public const int MaxValue = ushort.MaxValue;

    /// <summary>The maximum number of bytes a compact-u16 value occupies (3).</summary>
    public const int MaxEncodedLength = 3;

    /// <summary>Returns how many bytes <paramref name="value"/> occupies when compact-u16 encoded.</summary>
    /// <param name="value">The value to measure, in [0, <see cref="MaxValue"/>].</param>
    /// <returns>The encoded length: 1, 2, or 3 bytes.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside [0, <see cref="MaxValue"/>].</exception>
    public static int GetByteCount(int value)
    {
        ThrowIfOutOfRange(value);
        return value < 0x80 ? 1 : value < 0x4000 ? 2 : 3;
    }

    /// <summary>Writes the compact-u16 encoding of <paramref name="value"/> into <paramref name="destination"/>.</summary>
    /// <param name="value">The value to encode, in [0, <see cref="MaxValue"/>].</param>
    /// <param name="destination">The span to write into; must be at least <see cref="GetByteCount(int)"/> bytes (3 is always enough).</param>
    /// <returns>The number of bytes written.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside [0, <see cref="MaxValue"/>].</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is too small to hold the encoding.</exception>
    public static int Encode(int value, Span<byte> destination)
    {
        var needed = GetByteCount(value);
        if (destination.Length < needed)
            throw new ArgumentException($"Destination must be at least {needed} bytes.", nameof(destination));

        var v = (uint)value;
        var i = 0;
        while (true)
        {
            var b = (byte)(v & 0x7F);
            v >>= 7;
            if (v == 0)
            {
                destination[i++] = b;
                return i;
            }

            destination[i++] = (byte)(b | 0x80);
        }
    }

    /// <summary>Returns the compact-u16 encoding of <paramref name="value"/> as a new array.</summary>
    /// <param name="value">The value to encode, in [0, <see cref="MaxValue"/>].</param>
    /// <returns>A 1-to-3 byte array holding the encoding.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is outside [0, <see cref="MaxValue"/>].</exception>
    public static byte[] Encode(int value)
    {
        Span<byte> buffer = stackalloc byte[MaxEncodedLength];
        var written = Encode(value, buffer);
        return buffer[..written].ToArray();
    }

    /// <summary>Reads one compact-u16 value from the start of <paramref name="source"/>.</summary>
    /// <param name="source">The bytes to read from; only the leading encoded value is consumed, trailing bytes are ignored.</param>
    /// <param name="bytesRead">The number of bytes the encoding occupied.</param>
    /// <returns>The decoded value, in [0, <see cref="MaxValue"/>].</returns>
    /// <exception cref="FormatException">
    /// The input ends mid-value, runs longer than <see cref="MaxEncodedLength"/> bytes, exceeds the u16 range,
    /// or is not the minimal encoding of its value.
    /// </exception>
    public static int Decode(ReadOnlySpan<byte> source, out int bytesRead)
    {
        var value = 0;
        var shift = 0;
        bytesRead = 0;

        while (true)
        {
            if (bytesRead >= source.Length)
                throw new FormatException("shortvec: unexpected end of input.");
            if (bytesRead >= MaxEncodedLength)
                throw new FormatException("shortvec: encoding longer than 3 bytes.");

            var b = source[bytesRead++];
            value |= (b & 0x7F) << shift;

            if ((b & 0x80) == 0)
                break;

            shift += 7;
        }

        if (value > MaxValue)
            throw new FormatException("shortvec: value out of u16 range.");
        if (GetByteCount(value) != bytesRead)
            throw new FormatException("shortvec: non-minimal encoding.");

        return value;
    }

    private static void ThrowIfOutOfRange(int value)
    {
        if (value is < 0 or > MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value), value, "shortvec value must be in [0, 65535].");
    }
}
