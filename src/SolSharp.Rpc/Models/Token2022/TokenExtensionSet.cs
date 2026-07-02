using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models.Token2022;

/// <summary>
/// The decoded Token-2022 extension section of a mint or token account. An extended account is padded to
/// the 165-byte token-account length, carries a 1-byte account type at offset 165, and stores extensions
/// from offset 166 as TLV entries (a <c>u16</c> type, a <c>u16</c> length, then the value). Decode with
/// <see cref="DecodeMint"/> / <see cref="DecodeAccount"/> on the raw bytes <c>getAccountInfo</c> returns,
/// alongside the base <see cref="Mint"/> / <see cref="TokenAccount"/> decoders.
/// </summary>
public sealed record TokenExtensionSet
{
    // Layout constants from spl_token_2022_interface::extension: the account type follows the padded
    // 165-byte base, and the TLV data follows the account type.
    private const int AccountTypeIndex = 165;
    private const int TlvStartIndex = AccountTypeIndex + 1;

    private const byte MintAccountType = 1;
    private const byte TokenAccountType = 2;

    /// <summary>Every TLV entry present, in on-chain order, including types this library has no typed view for.</summary>
    public required IReadOnlyList<TokenExtension> Extensions { get; init; }

    /// <summary>Whether an extension of <paramref name="type"/> is present.</summary>
    /// <param name="type">The extension type to look for.</param>
    /// <returns><c>true</c> when present.</returns>
    public bool Has(ExtensionType type) => Find(type) is not null;

    /// <summary>Returns the raw value bytes of the first extension of <paramref name="type"/>, or <c>null</c> if absent.</summary>
    /// <param name="type">The extension type to look for.</param>
    /// <returns>The extension's value bytes, or <c>null</c>.</returns>
    public byte[]? Find(ExtensionType type)
    {
        foreach (var extension in Extensions)
            if (extension.Type == type)
                return extension.Data;

        return null;
    }

    /// <summary>Decodes the extension section of a Token-2022 mint account.</summary>
    /// <param name="data">The mint account's raw data.</param>
    /// <returns>
    /// The extension set - empty for a bare 82-byte mint - or <c>null</c> if the data is not a mint
    /// (too short, a wrong account-type byte, or malformed TLV data).
    /// </returns>
    public static TokenExtensionSet? DecodeMint(ReadOnlySpan<byte> data)
        => Decode(data, Mint.Length, MintAccountType);

    /// <summary>Decodes the extension section of a Token-2022 token account.</summary>
    /// <param name="data">The token account's raw data.</param>
    /// <returns>
    /// The extension set - empty for a bare 165-byte account - or <c>null</c> if the data is not a token
    /// account (too short, a wrong account-type byte, or malformed TLV data).
    /// </returns>
    public static TokenExtensionSet? DecodeAccount(ReadOnlySpan<byte> data)
        => Decode(data, TokenAccount.Length, TokenAccountType);

    /// <summary>The mint's transfer-fee schedule and authorities, or <c>null</c> when the extension is absent.</summary>
    /// <returns>The decoded <see cref="Token2022.TransferFeeConfig"/>, or <c>null</c>.</returns>
    public TransferFeeConfig? GetTransferFeeConfig()
        => Find(ExtensionType.TransferFeeConfig) is { Length: >= TransferFeeConfig.Length } data
            ? TransferFeeConfig.Decode(data)
            : null;

    /// <summary>The fees withheld on a token account (<see cref="ExtensionType.TransferFeeAmount"/>), or <c>null</c> when absent.</summary>
    /// <returns>The withheld amount in base units, or <c>null</c>.</returns>
    public ulong? GetWithheldTransferFee()
        => Find(ExtensionType.TransferFeeAmount) is { Length: >= 8 } data
            ? BinaryPrimitives.ReadUInt64LittleEndian(data)
            : null;

    /// <summary>The mint's close authority (<see cref="ExtensionType.MintCloseAuthority"/>), or <c>null</c> when absent or unset.</summary>
    /// <returns>The close authority, or <c>null</c>.</returns>
    public PublicKey? GetMintCloseAuthority()
        => Find(ExtensionType.MintCloseAuthority) is { Length: >= PublicKey.Length } data
            ? Token2022Layout.ReadOptionalKey(data)
            : null;

    /// <summary>The mint's permanent delegate (<see cref="ExtensionType.PermanentDelegate"/>), or <c>null</c> when absent or unset.</summary>
    /// <returns>The permanent delegate, or <c>null</c>.</returns>
    public PublicKey? GetPermanentDelegate()
        => Find(ExtensionType.PermanentDelegate) is { Length: >= PublicKey.Length } data
            ? Token2022Layout.ReadOptionalKey(data)
            : null;

    /// <summary>The default state for new accounts of the mint (<see cref="ExtensionType.DefaultAccountState"/>), or <c>null</c> when absent.</summary>
    /// <returns>The default account state, or <c>null</c>.</returns>
    public TokenAccountState? GetDefaultAccountState()
        => Find(ExtensionType.DefaultAccountState) is { Length: >= 1 } data
            ? (TokenAccountState)data[0]
            : null;

    /// <summary>Whether the account requires inbound transfers to carry a memo (<see cref="ExtensionType.MemoTransfer"/>), or <c>null</c> when absent.</summary>
    /// <returns><c>true</c> / <c>false</c> from the extension, or <c>null</c>.</returns>
    public bool? GetMemoTransferRequired()
        => Find(ExtensionType.MemoTransfer) is { Length: >= 1 } data
            ? data[0] != 0
            : null;

    /// <summary>The mint's metadata pointer (<see cref="ExtensionType.MetadataPointer"/>), or <c>null</c> when absent.</summary>
    /// <returns>The decoded <see cref="Token2022.MetadataPointer"/>, or <c>null</c>.</returns>
    public MetadataPointer? GetMetadataPointer()
        => Find(ExtensionType.MetadataPointer) is { Length: >= MetadataPointer.Length } data
            ? MetadataPointer.Decode(data)
            : null;

    /// <summary>The mint's in-mint token metadata (<see cref="ExtensionType.TokenMetadata"/>), or <c>null</c> when absent or malformed.</summary>
    /// <returns>The decoded <see cref="Token2022.TokenMetadata"/>, or <c>null</c>.</returns>
    public TokenMetadata? GetTokenMetadata()
    {
        var data = Find(ExtensionType.TokenMetadata);
        if (data is null || data.Length < PublicKey.Length * 2)
            return null;

        try
        {
            return TokenMetadata.Decode(data);
        }
        catch (FormatException)
        {
            // Variable-length Borsh content; a corrupt entry reads as "no metadata" rather than throwing.
            return null;
        }
    }

    private static TokenExtensionSet? Decode(ReadOnlySpan<byte> data, int baseLength, byte expectedAccountType)
    {
        if (data.Length < baseLength)
            return null;

        // A bare (non-extended) account is exactly the base length and has no extension section.
        if (data.Length == baseLength)
            return new TokenExtensionSet { Extensions = [] };

        if (data.Length < TlvStartIndex || data[AccountTypeIndex] != expectedAccountType)
            return null;

        var extensions = new List<TokenExtension>();
        var offset = TlvStartIndex;
        while (offset + 2 <= data.Length)
        {
            var type = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
            if (type == (ushort)ExtensionType.Uninitialized)
                break; // zero padding marks the end of the TLV data

            if (offset + 4 > data.Length)
                return null;

            var length = BinaryPrimitives.ReadUInt16LittleEndian(data[(offset + 2)..]);
            var valueStart = offset + 4;
            if (valueStart + length > data.Length)
                return null;

            extensions.Add(new TokenExtension
            {
                Type = (ExtensionType)type,
                Data = data.Slice(valueStart, length).ToArray()
            });
            offset = valueStart + length;
        }

        return new TokenExtensionSet { Extensions = extensions };
    }
}
