using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Token2022;

/// <summary>
/// The mint's <see cref="ExtensionType.TokenMetadata"/> extension: name, symbol, URI, and free-form
/// key/value pairs, Borsh-encoded per the <c>spl-token-metadata-interface</c>.
/// </summary>
public sealed record TokenMetadata
{
    /// <summary>The authority allowed to update the metadata, or <c>null</c> if it is immutable.</summary>
    public required PublicKey? UpdateAuthority { get; init; }

    /// <summary>The mint the metadata describes.</summary>
    public required PublicKey Mint { get; init; }

    /// <summary>The token's display name.</summary>
    public required string Name { get; init; }

    /// <summary>The token's ticker symbol.</summary>
    public required string Symbol { get; init; }

    /// <summary>The URI of the token's off-chain metadata (image, description, ...).</summary>
    public required string Uri { get; init; }

    /// <summary>Additional free-form key/value pairs, in on-chain order.</summary>
    public required IReadOnlyList<KeyValuePair<string, string>> AdditionalMetadata { get; init; }

    internal static TokenMetadata Decode(ReadOnlySpan<byte> data)
    {
        // The update authority is an OptionalNonZeroPubkey (raw 32 bytes, zero = none), not a Borsh Option;
        // everything after it is plain Borsh.
        var updateAuthority = Token2022Layout.ReadOptionalKey(data);

        var reader = new BorshReader(data[PublicKey.Length..]);
        var mint = reader.ReadPublicKey();
        var name = reader.ReadString();
        var symbol = reader.ReadString();
        var uri = reader.ReadString();

        var count = reader.ReadLength();
        var additional = new List<KeyValuePair<string, string>>(count);
        for (var i = 0; i < count; i++)
            additional.Add(new KeyValuePair<string, string>(reader.ReadString(), reader.ReadString()));

        return new TokenMetadata
        {
            UpdateAuthority = updateAuthority,
            Mint = mint,
            Name = name,
            Symbol = symbol,
            Uri = uri,
            AdditionalMetadata = additional
        };
    }
}
