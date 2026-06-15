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

    public static string Encode(ReadOnlySpan<byte> bytes) => Codec.Encode(bytes);

    public static byte[] Decode(string text) => Codec.Decode(text);

    /// <summary>Non-throwing decode. Returns false for null, empty or non-alphabet input.</summary>
    public static bool TryDecode(string? text, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrEmpty(text) || text.AsSpan().ContainsAnyExcept(Alphabet))
            return false;

        bytes = Codec.Decode(text);
        return true;
    }
}
