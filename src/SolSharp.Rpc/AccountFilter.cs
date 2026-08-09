using System.Text.Json.Serialization;
using SolSharp.Core.Encoding;

namespace SolSharp.Rpc;

/// <summary>
/// A <c>getProgramAccounts</c> / <c>programSubscribe</c> filter. Build one with <see cref="MemoryCompare"/>
/// (the legacy base58 <c>memcmp</c> factory), one of the explicitly encoded <c>memcmp</c> factories,
/// <see cref="DataSize"/>, or <see cref="TokenAccountState"/>. An account must satisfy every supplied
/// filter to be returned.
/// </summary>
public sealed class AccountFilter
{
    private const int MaxMemoryCompareBytes = 128;
    private const int MaxBase58Length = 175;
    private const int MaxBase64Length = 172;

    private AccountFilter(object payload) => Payload = payload;

    internal object Payload { get; }

    /// <summary>Matches accounts whose data at <paramref name="offset"/> equals <paramref name="bytesBase58"/> (a <c>memcmp</c> filter).</summary>
    /// <param name="offset">The byte offset into the account data to compare from.</param>
    /// <param name="bytesBase58">The bytes to match, base58-encoded.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="bytesBase58"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytesBase58"/> is not valid base58 or decodes to more than 128 bytes.
    /// </exception>
    public static AccountFilter MemoryCompare(int offset, string bytesBase58)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "The memory-compare offset cannot be negative.");

        return MemoryCompareBase58((ulong)offset, bytesBase58);
    }

    /// <summary>Matches base58-encoded bytes at an unsigned 64-bit account-data offset.</summary>
    /// <param name="offset">The byte offset into the account data to compare from.</param>
    /// <param name="bytesBase58">The bytes to match, base58-encoded.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bytesBase58"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytesBase58"/> is not valid base58 or decodes to more than 128 bytes.
    /// </exception>
    public static AccountFilter MemoryCompareBase58(ulong offset, string bytesBase58)
    {
        ValidateBase58(bytesBase58);
        return EncodedMemoryCompare(offset, bytesBase58, "base58");
    }

    /// <summary>Matches base64-encoded bytes at an unsigned 64-bit account-data offset.</summary>
    /// <param name="offset">The byte offset into the account data to compare from.</param>
    /// <param name="bytesBase64">The bytes to match, canonical base64-encoded.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bytesBase64"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="bytesBase64"/> is not canonical base64 or decodes to more than 128 bytes.
    /// </exception>
    public static AccountFilter MemoryCompareBase64(ulong offset, string bytesBase64)
    {
        ValidateBase64(bytesBase64);
        return EncodedMemoryCompare(offset, bytesBase64, "base64");
    }

    /// <summary>Matches raw bytes at an unsigned 64-bit account-data offset.</summary>
    /// <param name="offset">The byte offset into the account data to compare from.</param>
    /// <param name="bytes">The raw bytes to match.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentException"><paramref name="bytes"/> contains more than 128 bytes.</exception>
    public static AccountFilter MemoryCompareRaw(ulong offset, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > MaxMemoryCompareBytes)
        {
            throw new ArgumentException(
                $"Memory-compare data cannot exceed {MaxMemoryCompareBytes} bytes.", nameof(bytes));
        }

        return new AccountFilter(
            new RawMemcmpFilter
            {
                Memcmp = new RawMemcmpMatch
                {
                    Offset = offset,
                    Bytes = bytes.ToArray(),
                    Encoding = "bytes"
                }
            });
    }

    /// <summary>Matches accounts whose data is exactly <paramref name="size"/> bytes long (a <c>dataSize</c> filter).</summary>
    /// <param name="size">The required account data length in bytes.</param>
    /// <returns>The filter.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public static AccountFilter DataSize(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "The account-data size cannot be negative.");

        return DataSizeUnsigned((ulong)size);
    }

    /// <summary>Matches an exact account-data length across the full unsigned 64-bit upstream range.</summary>
    /// <param name="size">The required account data length in bytes.</param>
    /// <returns>The filter.</returns>
    public static AccountFilter DataSizeUnsigned(ulong size) =>
        new(new DataSizeFilter { DataSize = size });

    /// <summary>Matches data with a valid SPL Token or Token-2022 account state layout.</summary>
    /// <returns>The upstream <c>tokenAccountState</c> filter.</returns>
    public static AccountFilter TokenAccountState() => new("tokenAccountState");

    private static AccountFilter EncodedMemoryCompare(ulong offset, string bytes, string encoding) =>
        new(new MemcmpFilter
        {
            Memcmp = new MemcmpMatch { Offset = offset, Bytes = bytes, Encoding = encoding }
        });

    private static void ValidateBase58(string bytesBase58)
    {
        ArgumentNullException.ThrowIfNull(bytesBase58);
        if (bytesBase58.Length > MaxBase58Length)
            throw MemoryCompareTooLarge(nameof(bytesBase58));

        byte[] decoded;
        try
        {
            decoded = Base58.Decode(bytesBase58);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Memory-compare data must be valid base58.", nameof(bytesBase58), exception);
        }

        if (decoded.Length > MaxMemoryCompareBytes)
            throw MemoryCompareTooLarge(nameof(bytesBase58));
    }

    private static void ValidateBase64(string bytesBase64)
    {
        ArgumentNullException.ThrowIfNull(bytesBase64);
        if (bytesBase64.Length > MaxBase64Length)
            throw MemoryCompareTooLarge(nameof(bytesBase64));

        for (var i = 0; i < bytesBase64.Length; i++)
        {
            if (char.IsWhiteSpace(bytesBase64[i]))
                throw new ArgumentException("Memory-compare data must be canonical base64.", nameof(bytesBase64));
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(bytesBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Memory-compare data must be valid base64.", nameof(bytesBase64), exception);
        }

        if (!string.Equals(Convert.ToBase64String(decoded), bytesBase64, StringComparison.Ordinal))
            throw new ArgumentException("Memory-compare data must be canonical base64.", nameof(bytesBase64));
        if (decoded.Length > MaxMemoryCompareBytes)
            throw MemoryCompareTooLarge(nameof(bytesBase64));
    }

    private static ArgumentException MemoryCompareTooLarge(string parameterName) =>
        new($"Memory-compare data cannot exceed {MaxMemoryCompareBytes} decoded bytes.", parameterName);
}

/// <summary>The <c>{ memcmp: { offset, bytes, encoding } }</c> wire shape of a memcmp filter entry.</summary>
internal sealed record MemcmpFilter
{
    [JsonPropertyName("memcmp")]
    public required MemcmpMatch Memcmp { get; init; }
}

/// <summary>The body of a <see cref="MemcmpFilter"/>.</summary>
internal sealed record MemcmpMatch
{
    [JsonPropertyName("offset")]
    public required ulong Offset { get; init; }

    [JsonPropertyName("bytes")]
    public required string Bytes { get; init; }

    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }
}

/// <summary>The <c>{ memcmp: { offset, bytes: [...], encoding: "bytes" } }</c> raw-byte filter shape.</summary>
internal sealed record RawMemcmpFilter
{
    [JsonPropertyName("memcmp")]
    public required RawMemcmpMatch Memcmp { get; init; }
}

/// <summary>The raw-byte body of a <see cref="RawMemcmpFilter"/>.</summary>
internal sealed record RawMemcmpMatch
{
    [JsonPropertyName("offset")]
    public required ulong Offset { get; init; }

    [JsonPropertyName("bytes")]
    public required IReadOnlyList<byte> Bytes { get; init; }

    [JsonPropertyName("encoding")]
    public required string Encoding { get; init; }
}

/// <summary>The <c>{ dataSize }</c> wire shape of a data-size filter entry.</summary>
internal sealed record DataSizeFilter
{
    [JsonPropertyName("dataSize")]
    public required ulong DataSize { get; init; }
}
