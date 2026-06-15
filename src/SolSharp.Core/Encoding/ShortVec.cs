namespace SolSharp.Core.Encoding;

/// <summary>
/// compact-u16 ("shortvec"): a value in [0, 65535] encoded in 1-3 bytes, 7 bits per
/// byte, little-endian, high bit = continuation. Solana prefixes every wire-format
/// array (signatures, accounts, instructions) with this length.
/// </summary>
public static class ShortVec
{
    public const int MaxValue = ushort.MaxValue; // 65535
    public const int MaxEncodedLength = 3;

    public static int GetByteCount(int value)
    {
        ThrowIfOutOfRange(value);
        return value < 0x80 ? 1 : value < 0x4000 ? 2 : 3;
    }

    public static int Encode(int value, Span<byte> destination)
    {
        ThrowIfOutOfRange(value);

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

    public static byte[] Encode(int value)
    {
        Span<byte> buffer = stackalloc byte[MaxEncodedLength];
        var written = Encode(value, buffer);
        return buffer[..written].ToArray();
    }

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
