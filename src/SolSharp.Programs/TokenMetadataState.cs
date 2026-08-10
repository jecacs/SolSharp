using System.Buffers.Binary;
using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>A decoded SPL token-metadata interface value.</summary>
public sealed class TokenMetadataState
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    internal TokenMetadataState(
        PublicKey? updateAuthority,
        PublicKey mint,
        string name,
        string symbol,
        string uri,
        IReadOnlyList<KeyValuePair<string, string>> additionalMetadata)
    {
        UpdateAuthority = updateAuthority;
        Mint = mint;
        Name = name;
        Symbol = symbol;
        Uri = uri;
        AdditionalMetadata = additionalMetadata;
    }

    /// <summary>The metadata update authority, or <c>null</c> for immutable metadata.</summary>
    public PublicKey? UpdateAuthority { get; }

    /// <summary>The mint described by the metadata.</summary>
    public PublicKey Mint { get; }

    /// <summary>The display name.</summary>
    public string Name { get; }

    /// <summary>The short symbol.</summary>
    public string Symbol { get; }

    /// <summary>The off-chain metadata URI.</summary>
    public string Uri { get; }

    /// <summary>Additional key/value entries in encoded order.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> AdditionalMetadata { get; }

    /// <summary>Decodes the Borsh value stored in an SPL token-metadata TLV entry.</summary>
    /// <param name="data">The extension value without its TLV type/length header.</param>
    /// <returns>The decoded metadata, or <c>null</c> when the Borsh value is malformed.</returns>
    public static TokenMetadataState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < PublicKey.Length * 2)
            return null;
        var updateBytes = data[..PublicKey.Length];
        PublicKey? updateAuthority = updateBytes.IndexOfAnyExcept((byte)0) < 0 ? null : new PublicKey(updateBytes);
        var mint = new PublicKey(data.Slice(PublicKey.Length, PublicKey.Length));
        var offset = PublicKey.Length * 2;
        if (!TryReadString(data, ref offset, out var name) ||
            !TryReadString(data, ref offset, out var symbol) ||
            !TryReadString(data, ref offset, out var uri) ||
            data.Length - offset < sizeof(uint))
        {
            return null;
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        offset += sizeof(uint);
        const int minimumEntryLength = sizeof(uint) * 2;
        var maximumEntriesInRemainingData = (uint)((data.Length - offset) / minimumEntryLength);
        if (count > int.MaxValue || count > maximumEntriesInRemainingData)
            return null;
        var additional = new List<KeyValuePair<string, string>>((int)count);
        for (var i = 0; i < count; i++)
        {
            if (!TryReadString(data, ref offset, out var key) || !TryReadString(data, ref offset, out var value))
                return null;
            additional.Add(new(key, value));
        }

        return new(updateAuthority, mint, name, symbol, uri, additional);
    }

    internal static bool TryReadString(ReadOnlySpan<byte> data, ref int offset, out string value)
    {
        if (offset < 0 || data.Length - offset < sizeof(uint))
        {
            value = string.Empty;
            return false;
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        offset += sizeof(uint);
        if (length > int.MaxValue || length > data.Length - offset)
        {
            value = string.Empty;
            return false;
        }

        try
        {
            value = StrictUtf8.GetString(data.Slice(offset, (int)length));
        }
        catch (DecoderFallbackException)
        {
            value = string.Empty;
            return false;
        }

        offset += (int)length;
        return true;
    }
}
