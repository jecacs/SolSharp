using System.Buffers;
using System.Buffers.Binary;
using SbBase58 = SimpleBase.Base58;

namespace SolSharp.Core.Encoding;

/// <summary>
/// Base58 on the Bitcoin alphabet - the encoding Solana uses for public keys,
/// signatures and blockhashes. Single wrapper so nothing else references SimpleBase directly.
/// </summary>
public static class Base58
{
    // Public keys and hashes are always this size, and they dominate every parse-heavy workload, so they
    // get a fixed-width codec instead of SimpleBase's general byte-at-a-time bignum.
    private const int KeyLength = 32;

    private const string AlphabetText = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    private static readonly SbBase58 Codec = SbBase58.Bitcoin;

    private static readonly SearchValues<char> Alphabet = SearchValues.Create(AlphabetText);

    private static readonly sbyte[] DigitValues = BuildDigitValues();

    /// <summary>Encodes the given bytes as a base58 string on the Bitcoin alphabet.</summary>
    /// <param name="bytes">The bytes to encode. An empty span yields an empty string.</param>
    /// <returns>The base58-encoded string.</returns>
    public static string Encode(ReadOnlySpan<byte> bytes)
        => bytes.Length == KeyLength ? Encode32(bytes) : Codec.Encode(bytes);

    /// <summary>Decodes a base58 string (Bitcoin alphabet) into its raw bytes.</summary>
    /// <param name="text">The base58 string to decode. An empty string yields an empty array.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="text"/> contains characters outside the base58 alphabet.
    /// Use <see cref="TryDecode(string?, out byte[])"/> for input that may be malformed.
    /// </exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return [];
        if (text.ContainsAnyExcept(Alphabet))
            throw new FormatException($"Not a valid base58 string (input length {text.Length}).");

        return Codec.Decode(text);
    }

    /// <summary>
    /// Decodes a base58 string whose length is bounded before any decoding work is done. Base58 decoding
    /// is quadratic in the input length, so fixed-width values reject over-long input up front rather than
    /// decoding a hostile string only to discard it.
    /// </summary>
    /// <param name="text">The base58 string to decode. An empty string yields an empty array.</param>
    /// <param name="maxLength">The largest accepted number of base58 characters.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is negative.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="text"/> is longer than <paramref name="maxLength"/> or contains characters outside
    /// the base58 alphabet.
    /// </exception>
    public static byte[] Decode(string text, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        if (text.Length > maxLength)
            throw new FormatException($"Base58 string is {text.Length} characters, at most {maxLength} are accepted.");

        return Decode(text);
    }

    /// <summary>Non-throwing decode. Returns false for null, empty or non-alphabet input.</summary>
    /// <param name="text">The base58 string to decode, or <c>null</c>.</param>
    /// <param name="bytes">The decoded bytes on success; an empty array otherwise.</param>
    /// <returns><c>true</c> if <paramref name="text"/> was non-empty and fully within the base58 alphabet.</returns>
    public static bool TryDecode(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.ContainsAnyExcept(Alphabet))
            return false;

        bytes = Codec.Decode(text);
        return true;
    }

    /// <summary>
    /// Non-throwing decode that rejects over-long input before decoding it. Base58 decoding is quadratic
    /// in the input length, so this keeps hostile strings from costing work proportional to their size.
    /// </summary>
    /// <param name="text">The base58 string to decode, or <c>null</c>.</param>
    /// <param name="maxLength">The largest accepted number of base58 characters.</param>
    /// <param name="bytes">The decoded bytes on success; an empty array otherwise.</param>
    /// <returns><c>true</c> if <paramref name="text"/> was non-empty, within <paramref name="maxLength"/>, and fully within the base58 alphabet.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxLength"/> is negative.</exception>
    public static bool TryDecode(string? text, int maxLength, out byte[] bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxLength);
        bytes = [];
        return text is not null && text.Length <= maxLength && TryDecode(text, out bytes);
    }

    /// <summary>
    /// Decodes a base58 string that must represent exactly 32 bytes, writing straight into
    /// <paramref name="destination"/> so no intermediate array is allocated. Accepts exactly the same
    /// strings as the general decoder followed by a 32-byte length check.
    /// </summary>
    internal static bool TryDecode32(ReadOnlySpan<char> text, Span<byte> destination)
    {
        // 44 is the longest base58 string a 32-byte value can produce, so anything longer is rejected
        // before the quadratic accumulation starts.
        if (text.Length is 0 or > 44)
            return false;

        var zeros = 0;
        while (zeros < text.Length && text[zeros] == '1')
            zeros++;

        Span<uint> limbs = stackalloc uint[KeyLength / sizeof(uint)];
        limbs.Clear();

        for (var i = zeros; i < text.Length; i++)
        {
            var character = text[i];
            if (character > 127)
                return false;

            var digit = DigitValues[character];
            if (digit < 0)
                return false;

            var carry = (ulong)digit;
            for (var limb = limbs.Length - 1; limb >= 0; limb--)
            {
                var current = (limbs[limb] * 58UL) + carry;
                limbs[limb] = (uint)current;
                carry = current >> 32;
            }

            // A carry out of the top limb means the value needs more than 32 bytes.
            if (carry != 0)
                return false;
        }

        Span<byte> value = stackalloc byte[KeyLength];
        for (var limb = 0; limb < limbs.Length; limb++)
            BinaryPrimitives.WriteUInt32BigEndian(value[(limb * sizeof(uint))..], limbs[limb]);

        var firstSignificant = 0;
        while (firstSignificant < KeyLength && value[firstSignificant] == 0)
            firstSignificant++;

        // The general decoder emits one leading zero byte per leading '1' followed by the value's minimal
        // encoding, so it yields 32 bytes only when those two parts add up to exactly 32.
        if (zeros + (KeyLength - firstSignificant) != KeyLength)
            return false;

        value.CopyTo(destination);
        return true;
    }

    private static sbyte[] BuildDigitValues()
    {
        var values = new sbyte[128];
        Array.Fill(values, (sbyte)-1);
        for (var i = 0; i < AlphabetText.Length; i++)
            values[AlphabetText[i]] = (sbyte)i;

        return values;
    }

    private static string Encode32(ReadOnlySpan<byte> bytes)
    {
        var zeros = 0;
        while (zeros < KeyLength && bytes[zeros] == 0)
            zeros++;

        Span<uint> limbs = stackalloc uint[KeyLength / sizeof(uint)];
        for (var limb = 0; limb < limbs.Length; limb++)
            limbs[limb] = BinaryPrimitives.ReadUInt32BigEndian(bytes[(limb * sizeof(uint))..]);

        Span<char> buffer = stackalloc char[44];
        var position = buffer.Length;
        var start = 0;
        while (start < limbs.Length)
        {
            if (limbs[start] == 0)
            {
                start++;
                continue;
            }

            ulong remainder = 0;
            for (var limb = start; limb < limbs.Length; limb++)
            {
                var current = (remainder << 32) | limbs[limb];
                limbs[limb] = (uint)(current / 58UL);
                remainder = current % 58UL;
            }

            buffer[--position] = AlphabetText[(int)remainder];
        }

        for (var i = 0; i < zeros; i++)
            buffer[--position] = '1';

        return new(buffer[position..]);
    }
}
