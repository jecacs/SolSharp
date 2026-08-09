using System.Buffers.Binary;
using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>A single instruction embedded in Token-2022's compact batch payload.</summary>
public sealed class DecodedTokenBatchEntry
{
    private readonly byte[] _data;

    internal DecodedTokenBatchEntry(byte accountCount, ReadOnlySpan<byte> data)
    {
        AccountCount = accountCount;
        _data = data.ToArray();
    }

    /// <summary>The number of sequential account metas consumed by this embedded instruction.</summary>
    public byte AccountCount { get; }

    /// <summary>The complete embedded token instruction data.</summary>
    public ReadOnlyMemory<byte> Data => _data;
}

/// <summary>
/// A decoded SPL Token or Token-2022 instruction. Common fixed fields are exposed in typed form while
/// extension-specific POD bytes remain available through <see cref="Payload"/>.
/// </summary>
public sealed class DecodedTokenInstruction
{
    private readonly byte[] _payload;

    internal DecodedTokenInstruction(byte discriminator, string name, ReadOnlySpan<byte> payload)
    {
        Discriminator = discriminator;
        Name = name;
        _payload = payload.ToArray();
    }

    /// <summary>The outer one-byte token instruction discriminator.</summary>
    public byte Discriminator { get; }

    /// <summary>The upstream instruction variant name.</summary>
    public string Name { get; }

    /// <summary>All bytes after the outer discriminator.</summary>
    public ReadOnlyMemory<byte> Payload => _payload;

    /// <summary>The inner one-byte discriminator for a Token-2022 extension instruction, when present.</summary>
    public byte? ExtensionInstructionDiscriminator { get; internal set; }

    /// <summary>An amount decoded from a standard token instruction.</summary>
    public ulong? Amount { get; internal set; }

    /// <summary>Mint decimals decoded from a checked or initialize-mint instruction.</summary>
    public byte? Decimals { get; internal set; }

    /// <summary>A primary authority, owner, or delegate key encoded in instruction data.</summary>
    public PublicKey? RelatedPublicKey { get; internal set; }

    /// <summary>An optional freeze/new/close authority encoded in instruction data.</summary>
    public PublicKey? OptionalPublicKey { get; internal set; }

    /// <summary>Whether this variant contains an optional-public-key field, including when its value is null.</summary>
    public bool HasOptionalPublicKey { get; internal set; }

    /// <summary>The set-authority kind, when this is a SetAuthority instruction.</summary>
    public AuthorityType? AuthorityType { get; internal set; }

    /// <summary>The required signature count in an initialize-multisig instruction.</summary>
    public byte? RequiredSignatures { get; internal set; }

    /// <summary>The UI amount string in a UiAmountToAmount instruction.</summary>
    public string? UiAmount { get; internal set; }

    /// <summary>Extension types from Token-2022 GetAccountDataSize or Reallocate data.</summary>
    public IReadOnlyList<Token2022ExtensionType> ExtensionTypes { get; internal set; } = [];

    /// <summary>Whether this variant contains an optional raw amount, including when its value is null.</summary>
    public bool HasOptionalAmount { get; internal set; }

    /// <summary>The decoded entries of a Token-2022 compact batch.</summary>
    public IReadOnlyList<DecodedTokenBatchEntry> BatchEntries { get; internal set; } = [];
}

public static partial class TokenProgram
{
    private static readonly UTF8Encoding StrictInstructionUtf8 = new(false, true);

    /// <summary>Decodes SPL Token and Token-2022 instruction data using the pinned upstream wire layouts.</summary>
    /// <param name="data">Complete instruction data, beginning with the outer discriminator.</param>
    /// <returns>The decoded instruction, or <c>null</c> when the discriminator or required data is invalid.</returns>
    public static DecodedTokenInstruction? DecodeInstructionData(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return null;

        var discriminator = data[0];
        var payload = data[1..];
        var decoded = new DecodedTokenInstruction(discriminator, TokenInstructionName(discriminator), payload);
        switch (discriminator)
        {
            case 0:
            case 20:
                if (payload.Length < 1 + PublicKey.Length ||
                    !TryReadCompactOptionalPublicKey(payload[(1 + PublicKey.Length)..], out var freezeAuthority))
                {
                    return null;
                }

                decoded.Decimals = payload[0];
                decoded.RelatedPublicKey = new PublicKey(payload.Slice(1, PublicKey.Length));
                decoded.HasOptionalPublicKey = true;
                decoded.OptionalPublicKey = freezeAuthority;
                break;
            case 1:
            case 5:
            case 9:
            case 10:
            case 11:
            case 17:
            case 22:
            case 31:
            case 32:
            case 38:
                break;
            case 2:
            case 19:
                if (payload.IsEmpty)
                    return null;
                decoded.RequiredSignatures = payload[0];
                break;
            case 3:
            case 4:
            case 7:
            case 8:
            case 23:
                if (!TryReadAmount(payload, out var amount))
                    return null;
                decoded.Amount = amount;
                break;
            case 6:
                if (payload.IsEmpty ||
                    payload[0] > (byte)Programs.AuthorityType.PermissionedBurn ||
                    !TryReadCompactOptionalPublicKey(payload[1..], out var newAuthority))
                {
                    return null;
                }

                decoded.AuthorityType = (Programs.AuthorityType)payload[0];
                decoded.HasOptionalPublicKey = true;
                decoded.OptionalPublicKey = newAuthority;
                break;
            case 12:
            case 13:
            case 14:
            case 15:
                if (payload.Length < sizeof(ulong) + 1)
                    return null;
                decoded.Amount = BinaryPrimitives.ReadUInt64LittleEndian(payload);
                decoded.Decimals = payload[sizeof(ulong)];
                break;
            case 16:
            case 18:
            case 35:
                if (payload.Length < PublicKey.Length)
                    return null;
                decoded.RelatedPublicKey = new PublicKey(payload[..PublicKey.Length]);
                break;
            case 21:
            case 29:
                if (!TryReadExtensionTypes(payload, out var extensionTypes))
                    return null;
                decoded.ExtensionTypes = extensionTypes;
                break;
            case 24:
                try
                {
                    decoded.UiAmount = StrictInstructionUtf8.GetString(payload);
                }
                catch (DecoderFallbackException)
                {
                    return null;
                }

                break;
            case 25:
                if (!TryReadCompactOptionalPublicKey(payload, out var closeAuthority))
                    return null;
                decoded.HasOptionalPublicKey = true;
                decoded.OptionalPublicKey = closeAuthority;
                break;
            case 26:
            case 27:
            case 28:
            case 30:
            case 33:
            case 34:
            case 36:
            case 37:
            case 39:
            case 40:
            case 41:
            case 42:
            case 43:
            case 44:
            case 46:
                if (!payload.IsEmpty)
                    decoded.ExtensionInstructionDiscriminator = payload[0];
                break;
            case 45:
                if (!TryReadCompactOptionalAmount(payload, out var optionalAmount))
                    return null;
                decoded.HasOptionalAmount = true;
                decoded.Amount = optionalAmount;
                break;
            case 255:
                if (!TryReadBatchEntries(payload, out var entries))
                    return null;
                decoded.BatchEntries = entries;
                break;
            default:
                return null;
        }

        return decoded;
    }

    private static string TokenInstructionName(byte discriminator)
        => discriminator switch
        {
            0 => "InitializeMint",
            1 => "InitializeAccount",
            2 => "InitializeMultisig",
            3 => "Transfer",
            4 => "Approve",
            5 => "Revoke",
            6 => "SetAuthority",
            7 => "MintTo",
            8 => "Burn",
            9 => "CloseAccount",
            10 => "FreezeAccount",
            11 => "ThawAccount",
            12 => "TransferChecked",
            13 => "ApproveChecked",
            14 => "MintToChecked",
            15 => "BurnChecked",
            16 => "InitializeAccount2",
            17 => "SyncNative",
            18 => "InitializeAccount3",
            19 => "InitializeMultisig2",
            20 => "InitializeMint2",
            21 => "GetAccountDataSize",
            22 => "InitializeImmutableOwner",
            23 => "AmountToUiAmount",
            24 => "UiAmountToAmount",
            25 => "InitializeMintCloseAuthority",
            26 => "TransferFeeExtension",
            27 => "ConfidentialTransferExtension",
            28 => "DefaultAccountStateExtension",
            29 => "Reallocate",
            30 => "MemoTransferExtension",
            31 => "CreateNativeMint",
            32 => "InitializeNonTransferableMint",
            33 => "InterestBearingMintExtension",
            34 => "CpiGuardExtension",
            35 => "InitializePermanentDelegate",
            36 => "TransferHookExtension",
            37 => "ConfidentialTransferFeeExtension",
            38 => "WithdrawExcessLamports",
            39 => "MetadataPointerExtension",
            40 => "GroupPointerExtension",
            41 => "GroupMemberPointerExtension",
            42 => "ConfidentialMintBurnExtension",
            43 => "ScaledUiAmountExtension",
            44 => "PausableExtension",
            45 => "UnwrapLamports",
            46 => "PermissionedBurnExtension",
            255 => "Batch",
            _ => string.Empty
        };

    private static bool TryReadAmount(ReadOnlySpan<byte> data, out ulong amount)
    {
        if (data.Length < sizeof(ulong))
        {
            amount = 0;
            return false;
        }

        amount = BinaryPrimitives.ReadUInt64LittleEndian(data);
        return true;
    }

    private static bool TryReadCompactOptionalPublicKey(ReadOnlySpan<byte> data, out PublicKey? value)
    {
        if (data.IsEmpty)
        {
            value = null;
            return false;
        }

        if (data[0] == 0)
        {
            value = null;
            return true;
        }

        if (data[0] == 1 && data.Length >= 1 + PublicKey.Length)
        {
            value = new PublicKey(data.Slice(1, PublicKey.Length));
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadCompactOptionalAmount(ReadOnlySpan<byte> data, out ulong? value)
    {
        if (data.IsEmpty)
        {
            value = null;
            return false;
        }

        if (data[0] == 0)
        {
            value = null;
            return true;
        }

        if (data[0] == 1 && data.Length >= 1 + sizeof(ulong))
        {
            value = BinaryPrimitives.ReadUInt64LittleEndian(data[1..]);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryReadExtensionTypes(
        ReadOnlySpan<byte> data,
        out IReadOnlyList<Token2022ExtensionType> extensionTypes)
    {
        if (data.Length % sizeof(ushort) != 0)
        {
            extensionTypes = [];
            return false;
        }

        var decoded = new Token2022ExtensionType[data.Length / sizeof(ushort)];
        for (var i = 0; i < decoded.Length; i++)
        {
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data[(i * sizeof(ushort))..]);
            if (value > (ushort)Token2022ExtensionType.PermissionedBurn)
            {
                extensionTypes = [];
                return false;
            }

            decoded[i] = (Token2022ExtensionType)value;
        }

        extensionTypes = decoded;
        return true;
    }

    private static bool TryReadBatchEntries(
        ReadOnlySpan<byte> data,
        out IReadOnlyList<DecodedTokenBatchEntry> entries)
    {
        if (data.IsEmpty)
        {
            entries = [];
            return false;
        }

        var decoded = new List<DecodedTokenBatchEntry>();
        var offset = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 2)
            {
                entries = [];
                return false;
            }

            var accountCount = data[offset];
            var dataLength = data[offset + 1];
            offset += 2;
            if (dataLength == 0 || dataLength > data.Length - offset)
            {
                entries = [];
                return false;
            }

            decoded.Add(new DecodedTokenBatchEntry(accountCount, data.Slice(offset, dataLength)));
            offset += dataLength;
        }

        entries = decoded;
        return true;
    }
}
