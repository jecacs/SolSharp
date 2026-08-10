using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

public static partial class Token2022Program
{
    private static readonly byte[] InitializeMetadataDiscriminator = [0xd2, 0xe1, 0x1e, 0xa2, 0x58, 0xb8, 0x4d, 0x8d];
    private static readonly byte[] UpdateMetadataFieldDiscriminator = [0xdd, 0xe9, 0x31, 0x2d, 0xb5, 0xca, 0xdc, 0xc8];
    private static readonly byte[] RemoveMetadataKeyDiscriminator = [0xea, 0x12, 0x20, 0x38, 0x59, 0x8d, 0x25, 0xb5];
    private static readonly byte[] UpdateMetadataAuthorityDiscriminator = [0xd7, 0xe4, 0xa6, 0xe4, 0x54, 0x64, 0x56, 0x7b];
    private static readonly byte[] EmitMetadataDiscriminator = [0xfa, 0xa6, 0xb4, 0xfa, 0x0d, 0x0c, 0xb8, 0x46];

    /// <summary>Initializes token metadata through Token-2022's SPL token-metadata interface implementation.</summary>
    /// <param name="metadata">The writable metadata account; use the mint for in-mint metadata.</param>
    /// <param name="updateAuthority">The readonly authority allowed to update metadata.</param>
    /// <param name="mint">The readonly mint the metadata describes.</param>
    /// <param name="mintAuthority">The current mint authority; signs.</param>
    /// <param name="name">The token's display name.</param>
    /// <param name="symbol">The token's short symbol.</param>
    /// <param name="uri">The URI of the token's off-chain metadata.</param>
    /// <returns>The token-metadata initialize instruction.</returns>
    /// <exception cref="ArgumentNullException">A string argument is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A string contains invalid Unicode text.</exception>
    public static Instruction InitializeTokenMetadata(
        PublicKey metadata,
        PublicKey updateAuthority,
        PublicKey mint,
        PublicKey mintAuthority,
        string name,
        string symbol,
        string uri)
    {
        var writer = MetadataWriter(InitializeMetadataDiscriminator);
        writer.WriteString(name);
        writer.WriteString(symbol);
        writer.WriteString(uri);
        return new()
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(metadata),
                AccountMeta.Readonly(updateAuthority),
                AccountMeta.Readonly(mint),
                AccountMeta.ReadonlySigner(mintAuthority)
            ],
            Data = writer.ToArray()
        };
    }

    /// <summary>Updates a required field in Token-2022 token metadata.</summary>
    /// <param name="metadata">The writable metadata account.</param>
    /// <param name="updateAuthority">The current update authority; signs.</param>
    /// <param name="field">The required field to update.</param>
    /// <param name="value">The new field value.</param>
    /// <returns>The token-metadata update-field instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> contains invalid Unicode text.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="field"/> is not a defined required field.</exception>
    public static Instruction UpdateTokenMetadataField(
        PublicKey metadata,
        PublicKey updateAuthority,
        TokenMetadataField field,
        string value)
    {
        if ((byte)field > (byte)TokenMetadataField.Uri)
            throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown required token-metadata field.");
        return UpdateTokenMetadataFieldCore(metadata, updateAuthority, (byte)field, customKey: null, value);
    }

    /// <summary>Creates or updates a custom key/value field in Token-2022 token metadata.</summary>
    /// <param name="metadata">The writable metadata account.</param>
    /// <param name="updateAuthority">The current update authority; signs.</param>
    /// <param name="key">The custom field key.</param>
    /// <param name="value">The new field value.</param>
    /// <returns>The token-metadata update-field instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A string contains invalid Unicode text.</exception>
    public static Instruction UpdateTokenMetadataField(
        PublicKey metadata,
        PublicKey updateAuthority,
        string key,
        string value)
        => UpdateTokenMetadataFieldCore(metadata, updateAuthority, fieldTag: 3, key, value);

    /// <summary>Removes a custom key/value field from Token-2022 token metadata.</summary>
    /// <param name="metadata">The writable metadata account.</param>
    /// <param name="updateAuthority">The current update authority; signs.</param>
    /// <param name="key">The custom field key to remove.</param>
    /// <param name="idempotent">Whether a missing key should be treated as success.</param>
    /// <returns>The token-metadata remove-key instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> contains invalid Unicode text.</exception>
    public static Instruction RemoveTokenMetadataKey(
        PublicKey metadata,
        PublicKey updateAuthority,
        string key,
        bool idempotent = false)
    {
        var writer = MetadataWriter(RemoveMetadataKeyDiscriminator);
        writer.WriteBool(idempotent);
        writer.WriteString(key);
        return MetadataAuthorityInstruction(metadata, updateAuthority, writer.ToArray());
    }

    /// <summary>Changes or permanently removes the Token-2022 token-metadata update authority.</summary>
    /// <param name="metadata">The writable metadata account.</param>
    /// <param name="currentAuthority">The current update authority; signs.</param>
    /// <param name="newAuthority">The new authority, or <c>null</c> to make metadata immutable.</param>
    /// <returns>The token-metadata update-authority instruction.</returns>
    /// <exception cref="ArgumentException"><paramref name="newAuthority"/> is the all-zero address.</exception>
    public static Instruction UpdateTokenMetadataAuthority(
        PublicKey metadata,
        PublicKey currentAuthority,
        PublicKey? newAuthority)
    {
        if (newAuthority is { } authority && authority == default)
            throw new ArgumentException(
                "The all-zero address is reserved as the wire representation of null.",
                nameof(newAuthority));
        var writer = MetadataWriter(UpdateMetadataAuthorityDiscriminator);
        writer.WritePublicKey(newAuthority ?? default);
        return MetadataAuthorityInstruction(metadata, currentAuthority, writer.ToArray());
    }

    /// <summary>Requests all or a byte range of token metadata through program return data.</summary>
    /// <param name="metadata">The readonly metadata account.</param>
    /// <param name="start">The optional inclusive byte offset.</param>
    /// <param name="end">The optional exclusive byte offset.</param>
    /// <returns>The token-metadata emit instruction.</returns>
    public static Instruction EmitTokenMetadata(PublicKey metadata, ulong? start = null, ulong? end = null)
    {
        var writer = MetadataWriter(EmitMetadataDiscriminator);
        writer.WriteOption(start.HasValue);
        if (start is { } first)
            writer.WriteU64(first);
        writer.WriteOption(end.HasValue);
        if (end is { } last)
            writer.WriteU64(last);
        return new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Readonly(metadata)],
            Data = writer.ToArray()
        };
    }

    private static Instruction UpdateTokenMetadataFieldCore(
        PublicKey metadata,
        PublicKey updateAuthority,
        byte fieldTag,
        string? customKey,
        string value)
    {
        var writer = MetadataWriter(UpdateMetadataFieldDiscriminator);
        writer.WriteU8(fieldTag);
        if (customKey is not null)
            writer.WriteString(customKey);
        writer.WriteString(value);
        return MetadataAuthorityInstruction(metadata, updateAuthority, writer.ToArray());
    }

    private static BorshWriter MetadataWriter(ReadOnlySpan<byte> discriminator)
    {
        var writer = new BorshWriter();
        writer.WriteBytes(discriminator);
        return writer;
    }

    private static Instruction MetadataAuthorityInstruction(
        PublicKey metadata,
        PublicKey updateAuthority,
        byte[] data)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(metadata), AccountMeta.ReadonlySigner(updateAuthority)],
            Data = data
        };
}
