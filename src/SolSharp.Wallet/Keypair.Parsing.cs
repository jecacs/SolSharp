using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using SolSharp.Core.Encoding;

namespace SolSharp.Wallet;

public sealed partial class Keypair
{
    /// <summary>
    /// Parses a Solana secret key, auto-detecting the format: a JSON number array (the <c>id.json</c>
    /// written by <c>solana-keygen</c>, recognised by a leading <c>[</c>) or a base58 string (the form
    /// wallets export). Both a 32-byte seed and a 64-byte secret key are accepted.
    /// </summary>
    /// <param name="text">The secret key as a base58 string or a JSON number array.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty, or whitespace.</exception>
    /// <exception cref="FormatException"><paramref name="text"/> is not a recognised 32- or 64-byte key.</exception>
    public static Keypair Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return text.AsSpan().TrimStart()[0] == '['
            ? FromJsonArray(text)
            : FromBase58String(text);
    }

    /// <summary>Tries to parse a Solana secret key without throwing. See <see cref="Parse"/> for the accepted formats.</summary>
    /// <param name="text">The secret key as a base58 string or a JSON number array, or <c>null</c>.</param>
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

    private static Keypair FromDecoded(ReadOnlySpan<byte> bytes, string what)
        => bytes.Length switch
        {
            SecretKeyLength => FromSecretKey(bytes),
            SeedLength => FromSeed(bytes),
            _ => throw new FormatException(
                $"Expected a {SeedLength}- or {SecretKeyLength}-byte {what}, got {bytes.Length} bytes.")
        };
}
