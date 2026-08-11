using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using SolSharp.Core.Encoding;

namespace SolSharp.Wallet;

public sealed partial class Keypair
{
    private const int MaxBase58KeyLength = 88;
    private const int MaxJsonKeyLength = 4 * 1024;

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

        var trimmed = text.AsSpan().Trim();

        if (trimmed[0] == '[')
            return FromJsonArray(text);

        // Hex (optionally 0x-prefixed) is tried first: a pure-hex 64- or 128-char string is unambiguous,
        // because the same text read as base58 or base64 would not decode to a 32- or 64-byte key.
        if (TryDecodeHex(trimmed) is { } hex)
            return FromDecodedZeroing(hex, "hex key");

        // base58 (the wallet-export default) and base64 share an alphabet, so try base58 first and fall back
        // to base64, accepting whichever decodes to a 32- or 64-byte key.
        if (TryDecodeBase58(trimmed, text) is { } base58)
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

        var trimmed = base58.AsSpan().Trim();
        if (trimmed.Length > MaxBase58KeyLength)
            throw new FormatException("Key is not a valid base58 string.");

        var encoded = trimmed.Length == base58.Length ? base58 : trimmed.ToString();
        if (!Base58.TryDecode(encoded, MaxBase58KeyLength, out var bytes))
        {
            throw new FormatException("Key is not a valid base58 string.");
        }

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

        // Validating first yields the decoded length without decoding, which keeps the wrong-length case
        // reportable even though the buffer below is only large enough for a well-formed key.
        var trimmed = base64.AsSpan().Trim();
        if (!Base64.IsValid(trimmed, out var decodedLength))
            throw new FormatException("Key is not a valid base64 string.");

        if (decodedLength is not (SeedLength or SecretKeyLength))
        {
            throw new FormatException(
                $"Expected a {SeedLength}- or {SecretKeyLength}-byte base64 key, got {decodedLength} bytes.");
        }

        Span<byte> bytes = stackalloc byte[SecretKeyLength];
        try
        {
            if (!Convert.TryFromBase64Chars(trimmed, bytes, out var bytesWritten))
                throw new FormatException("Key is not a valid base64 string.");

            return FromDecoded(bytes[..bytesWritten], "base64 key");
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

        Span<byte> bytes = stackalloc byte[SecretKeyLength];
        try
        {
            var status = Convert.FromHexString(digits, bytes, out var charsConsumed, out var bytesWritten);

            // The buffer only fits a well-formed key, so valid hex that is merely too long comes back as
            // DestinationTooSmall. Reporting that as malformed hex would hide the real problem, and the
            // decoded length is derivable from the input without decoding it.
            if (status == OperationStatus.DestinationTooSmall)
            {
                throw new FormatException(
                    $"Expected a {SeedLength}- or {SecretKeyLength}-byte hex key, got {digits.Length / 2} bytes.");
            }

            if (status != OperationStatus.Done || charsConsumed != digits.Length)
                throw new FormatException("Key is not a valid hex string.");

            return FromDecoded(bytes[..bytesWritten], "hex key");
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
    /// <exception cref="FormatException"><paramref name="json"/> is too large, is not a JSON number array, holds a value outside 0-255, or is not 32 or 64 bytes long.</exception>
    public static Keypair FromJsonArray(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Length > MaxJsonKeyLength)
            throw new FormatException($"Key JSON cannot exceed {MaxJsonKeyLength} characters.");

        int[]? values;
        try
        {
            values = JsonSerializer.Deserialize(json, WalletJsonContext.Default.Int32Array);
        }
        catch (JsonException e)
        {
            throw new FormatException("Key is not a valid JSON number array.", e);
        }

        if (values is null)
            throw new FormatException("Key JSON must be an array, not null.");

        byte[]? bytes = null;
        try
        {
            bytes = new byte[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] is < 0 or > byte.MaxValue)
                    throw new FormatException($"Key JSON value at index {i} is outside the byte range 0-255.");

                bytes[i] = (byte)values[i];
            }

            return FromDecoded(bytes, "JSON key array");
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
            Array.Clear(values);
        }
    }

    /// <summary>
    /// Creates a keypair from a BIP-39 mnemonic the way <c>solana-keygen</c> does: the first
    /// <see cref="SeedLength"/> bytes of the BIP-39 seed become the Ed25519 seed, with no BIP-32 derivation.
    /// For the Phantom / Solflare derivation-path scheme use <see cref="FromMnemonicAtPath"/>.
    /// </summary>
    /// <param name="mnemonic">The mnemonic phrase (typically 12 or 24 space-separated words).</param>
    /// <param name="passphrase">The optional BIP-39 passphrase (the "25th word"); empty by default.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="mnemonic"/> is <c>null</c>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="passphrase"/> is <c>null</c>.</exception>
    public static Keypair FromMnemonic(string mnemonic, string passphrase = "")
    {
        var seed = Bip39.ToSeed(mnemonic, passphrase);
        try
        {
            return FromSeed(seed.AsSpan(0, SeedLength));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// Creates a keypair from a BIP-39 mnemonic with SLIP-0010 derivation, the way Phantom and Solflare
    /// derive accounts: <c>m/44'/501'/0'/0'</c> is the first account; increment the third segment for the
    /// next ones.
    /// </summary>
    /// <param name="mnemonic">The mnemonic phrase (typically 12 or 24 space-separated words).</param>
    /// <param name="derivationPath">The all-hardened derivation path, e.g. <c>m/44'/501'/0'/0'</c>.</param>
    /// <param name="passphrase">The optional BIP-39 passphrase (the "25th word"); empty by default.</param>
    /// <returns>The keypair.</returns>
    /// <exception cref="ArgumentException"><paramref name="mnemonic"/> is <c>null</c>, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="derivationPath"/> or <paramref name="passphrase"/> is <c>null</c>.</exception>
    /// <exception cref="FormatException"><paramref name="derivationPath"/> is not an all-hardened derivation path.</exception>
    public static Keypair FromMnemonicAtPath(string mnemonic, string derivationPath, string passphrase = "")
    {
        var seed = Bip39.ToSeed(mnemonic, passphrase);
        try
        {
            var derived = Slip10.DeriveEd25519(seed, derivationPath);
            try
            {
                return FromSeed(derived);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(derived);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// Exports the 64-byte secret key as the JSON number array used by <c>solana-keygen id.json</c>.
    /// The returned immutable string contains secret material and cannot be zeroed; prefer
    /// <see cref="ToBytes"/> when the receiving API accepts bytes.
    /// </summary>
    /// <returns>A JSON array containing the 32-byte seed followed by the 32-byte public key.</returns>
    /// <exception cref="ObjectDisposedException">The keypair has already been disposed.</exception>
    public string ToJsonArray()
    {
        var values = new int[SecretKeyLength];
        byte[]? bytes = null;
        try
        {
            bytes = ToBytes();
            for (var i = 0; i < bytes.Length; i++)
                values[i] = bytes[i];

            return JsonSerializer.Serialize(values, WalletJsonContext.Default.Int32Array);
        }
        finally
        {
            if (bytes is not null)
                CryptographicOperations.ZeroMemory(bytes);
            Array.Clear(values);
        }
    }

    private static byte[]? TryDecodeHex(ReadOnlySpan<char> text)
    {
        var digits = text;
        if (digits.StartsWith("0x") || digits.StartsWith("0X"))
            digits = digits[2..];

        if (digits.Length is not (SeedLength * 2 or SecretKeyLength * 2))
            return null;

        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            var status = Convert.FromHexString(digits, decoded, out var charsConsumed, out var bytesWritten);
            return status == OperationStatus.Done && charsConsumed == digits.Length
                ? decoded[..bytesWritten].ToArray()
                : null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    private static byte[]? TryDecodeBase58(ReadOnlySpan<char> text, string original)
    {
        if (text.Length > MaxBase58KeyLength)
            return null;

        var encoded = text.Length == original.Length ? original : text.ToString();
        if (!Base58.TryDecode(encoded, MaxBase58KeyLength, out var bytes))
        {
            return null;
        }

        if (bytes.Length is SeedLength or SecretKeyLength)
            return bytes;

        CryptographicOperations.ZeroMemory(bytes);
        return null;
    }

    private static byte[]? TryDecodeBase64(ReadOnlySpan<char> text)
    {
        Span<byte> decoded = stackalloc byte[SecretKeyLength];
        try
        {
            return TryDecodeBase64(text, decoded, out var bytesWritten)
                ? decoded[..bytesWritten].ToArray()
                : null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decoded);
        }
    }

    private static bool TryDecodeBase64(ReadOnlySpan<char> text, Span<byte> destination, out int bytesWritten)
    {
        if (!Convert.TryFromBase64Chars(text, destination, out bytesWritten))
            return false;

        return bytesWritten is SeedLength or SecretKeyLength;
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
