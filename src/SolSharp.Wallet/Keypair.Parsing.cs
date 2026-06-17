using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using SolSharp.Core.Encoding;

namespace SolSharp.Wallet;

public sealed partial class Keypair
{
    /// <summary>
    /// Parses a Solana secret key, auto-detecting the format: a JSON number array (the <c>id.json</c>
    /// written by <c>solana-keygen</c>, recognised by a leading <c>[</c>), a hex string (optionally
    /// <c>0x</c>-prefixed), a base58 string (the form wallets export), or base64. Both a 32-byte seed and a
    /// 64-byte secret key are accepted.
    /// </summary>
    /// <param name="text">The secret key as a JSON number array, hex, base58, or base64 string.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="text"/> is not a recognised 32- or 64-byte key.</exception>
    public static Keypair Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var trimmed = text.Trim();

        if (trimmed[0] == '[')
            return FromJsonArray(text);

        // Hex (optionally 0x-prefixed) is tried first: a pure-hex 64- or 128-char string is unambiguous,
        // because the same text read as base58 or base64 would not decode to a 32- or 64-byte key.
        if (TryDecodeHex(trimmed) is { } hex)
            return FromDecodedZeroing(hex, "hex key");

        // base58 (the wallet-export default) and base64 share an alphabet, so try base58 first and fall back
        // to base64, accepting whichever decodes to a 32- or 64-byte key.
        if (TryDecodeBase58(trimmed) is { } base58)
            return FromDecodedZeroing(base58, "base58 key");

        if (TryDecodeBase64(trimmed) is { } base64)
            return FromDecodedZeroing(base64, "base64 key");

        throw new FormatException(
            $"Key is not a recognised format; expected a JSON array, hex, base58, or base64 {SeedLength}- or {SecretKeyLength}-byte key.");
    }

    /// <summary>Tries to parse a Solana secret key without throwing. See <see cref="Parse"/> for the accepted formats.</summary>
    /// <param name="text">The secret key as a JSON number array, hex, base58, or base64 string, or <c>null</c>.</param>
    /// <param name="keypair">The parsed keypair on success; <c>null</c> otherwise.</param>
    /// <returns><c>true</c> if <paramref name="text"/> was a recognised key.</returns>
    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out Keypair? keypair)
    {
        try
        {
            keypair = string.IsNullOrWhiteSpace(text) ? null : Parse(text);
            return keypair is not null;
        }
        catch (Exception e) when (e is FormatException or ArgumentException)
        {
            keypair = null;
            return false;
        }
    }

    /// <summary>Creates a keypair from a base58-encoded 32-byte seed or 64-byte secret key.</summary>
    /// <param name="base58">The base58 string, as exported by Phantom and other wallets.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="base58"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="base58"/> is not valid base58, or does not decode to 32 or 64 bytes.</exception>
    public static Keypair FromBase58String(string base58)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base58);

        if (!Base58.TryDecode(base58.Trim(), out var bytes))
            throw new FormatException("Key is not a valid base58 string.");

        try
        {
            return FromDecoded(bytes, "base58 key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Creates a keypair from a base64-encoded 32-byte seed or 64-byte secret key.</summary>
    /// <param name="base64">The base64 string.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="base64"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="base64"/> is not valid base64, or does not decode to 32 or 64 bytes.</exception>
    public static Keypair FromBase64String(string base64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(base64);

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException e)
        {
            throw new FormatException("Key is not a valid base64 string.", e);
        }

        try
        {
            return FromDecoded(bytes, "base64 key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Creates a keypair from a hex-encoded 32-byte seed or 64-byte secret key; an optional <c>0x</c> prefix is accepted.</summary>
    /// <param name="hex">The hex string, with or without a leading <c>0x</c>.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="hex"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="hex"/> is not valid hex, or does not decode to 32 or 64 bytes.</exception>
    public static Keypair FromHexString(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        var digits = hex.AsSpan().Trim();
        if (digits.StartsWith("0x") || digits.StartsWith("0X"))
            digits = digits[2..];

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(digits.ToString());
        }
        catch (FormatException e)
        {
            throw new FormatException("Key is not a valid hex string.", e);
        }

        try
        {
            return FromDecoded(bytes, "hex key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    /// <summary>Creates a keypair from a JSON array of byte values (the <c>solana-keygen id.json</c> format).</summary>
    /// <param name="json">A JSON array of 32 or 64 integers, each in the range 0-255.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="json"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="json"/> is not a JSON number array, holds a value outside 0-255, or is not 32 or 64 bytes long.</exception>
    public static Keypair FromJsonArray(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        int[]? values;
        try
        {
            values = JsonSerializer.Deserialize<int[]>(json);
        }
        catch (JsonException e)
        {
            throw new FormatException("Key is not a valid JSON number array.", e);
        }

        if (values is null)
            throw new FormatException("Key JSON must be an array, not null.");

        var bytes = new byte[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] is < 0 or > byte.MaxValue)
                throw new FormatException($"Key JSON value at index {i} is outside the byte range 0-255: {values[i]}.");

            bytes[i] = (byte)values[i];
        }

        try
        {
            return FromDecoded(bytes, "JSON key array");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Array.Clear(values);
        }
    }

    // Returns the decoded key only when it is a valid 32- or 64-byte length, so Parse can fall through
    // to the next format rather than committing to a decoder that produced the wrong size.
    private static byte[]? TryDecodeHex(string text)
    {
        var digits = text.AsSpan();
        if (digits.StartsWith("0x") || digits.StartsWith("0X"))
            digits = digits[2..];

        // Only exactly 32- or 64-byte hex is treated as a key, so this never shadows a base58/base64 string.
        if (digits.Length is not (SeedLength * 2 or SecretKeyLength * 2))
            return null;

        try
        {
            return Convert.FromHexString(digits.ToString());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[]? TryDecodeBase58(string text)
        => Base58.TryDecode(text, out var bytes) && bytes.Length is SeedLength or SecretKeyLength ? bytes : null;

    private static byte[]? TryDecodeBase64(string text)
    {
        try
        {
            var bytes = Convert.FromBase64String(text);
            return bytes.Length is SeedLength or SecretKeyLength ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Keypair FromDecodedZeroing(byte[] bytes, string what)
    {
        try
        {
            return FromDecoded(bytes, what);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static Keypair FromDecoded(ReadOnlySpan<byte> bytes, string what)
        => bytes.Length switch
        {
            SecretKeyLength => FromSecretKey(bytes),
            SeedLength => FromSeed(bytes),
            _ => throw new FormatException(
                $"Expected a {SeedLength}- or {SecretKeyLength}-byte {what}, got {bytes.Length} bytes.")
        };
}
