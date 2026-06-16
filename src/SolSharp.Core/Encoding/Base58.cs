using System.Buffers;
using SbBase58 = SimpleBase.Base58;

namespace SolSharp.Core.Encoding;

/// <summary>
/// Base58 on the Bitcoin alphabet - the encoding Solana uses for public keys,
/// signatures and blockhashes. Single wrapper so nothing else references SimpleBase directly.
/// </summary>
public static class Base58
{
    private static readonly SbBase58 Codec = SbBase58.Bitcoin;

    private static readonly SearchValues<char> Alphabet =
        SearchValues.Create("123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz");

    /// <summary>Encodes the given bytes as a base58 string on the Bitcoin alphabet.</summary>
    /// <param name="bytes">The bytes to encode. An empty span yields an empty string.</param>
    /// <returns>The base58-encoded string.</returns>
    public static string Encode(ReadOnlySpan<byte> bytes) => Codec.Encode(bytes);

    /// <summary>Decodes a base58 string (Bitcoin alphabet) into its raw bytes.</summary>
    /// <param name="text">The base58 string to decode. An empty string yields an empty array.</param>
    /// <returns>The decoded bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException">
    /// <paramref name="text"/> contains characters outside the base58 alphabet.
    /// Use <see cref="TryDecode"/> for input that may be malformed.
    /// </exception>
    public static byte[] Decode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return [];
        if (text.AsSpan().ContainsAnyExcept(Alphabet))
            throw new FormatException($"Not a valid base58 string: '{text}'.");

        return Codec.Decode(text);
    }

    /// <summary>Non-throwing decode. Returns false for null, empty or non-alphabet input.</summary>
    /// <param name="text">The base58 string to decode, or <c>null</c>.</param>
    /// <param name="bytes">The decoded bytes on success; an empty array otherwise.</param>
    /// <returns><c>true</c> if <paramref name="text"/> was non-empty and fully within the base58 alphabet.</returns>
    public static bool TryDecode(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.AsSpan().ContainsAnyExcept(Alphabet))
            return false;

        bytes = Codec.Decode(text);
        return true;
    }
}
