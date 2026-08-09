using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>The initialization/freeze status stored in an SPL token account.</summary>
public enum TokenAccountStatus : byte
{
    /// <summary>The account has not been initialized.</summary>
    Uninitialized = 0,

    /// <summary>The account is initialized and usable.</summary>
    Initialized = 1,

    /// <summary>The account is frozen.</summary>
    Frozen = 2
}

/// <summary>An opaque Token-2022 extension value decoded from its two-byte type and length TLV header.</summary>
public sealed class Token2022ExtensionData
{
    private readonly byte[] _data;

    internal Token2022ExtensionData(Token2022ExtensionType extensionType, ReadOnlySpan<byte> data)
    {
        ExtensionType = extensionType;
        _data = data.ToArray();
    }

    /// <summary>The pinned Token-2022 extension type.</summary>
    public Token2022ExtensionType ExtensionType { get; }

    /// <summary>The exact extension value bytes, excluding the TLV type and length fields.</summary>
    public ReadOnlyMemory<byte> Data => _data;
}

/// <summary>A decoded classic SPL Token mint base state plus optional Token-2022 TLV extensions.</summary>
public sealed class TokenMintState
{
    /// <summary>The classic mint base-state length.</summary>
    public const int BaseLength = 82;

    internal TokenMintState(
        PublicKey? mintAuthority,
        ulong supply,
        byte decimals,
        bool isInitialized,
        PublicKey? freezeAuthority,
        IReadOnlyList<Token2022ExtensionData> extensions)
    {
        MintAuthority = mintAuthority;
        Supply = supply;
        Decimals = decimals;
        IsInitialized = isInitialized;
        FreezeAuthority = freezeAuthority;
        Extensions = extensions;
    }

    /// <summary>The mint authority, or <c>null</c> for fixed supply.</summary>
    public PublicKey? MintAuthority { get; }

    /// <summary>The raw token supply.</summary>
    public ulong Supply { get; }

    /// <summary>The number of decimal places.</summary>
    public byte Decimals { get; }

    /// <summary>Whether the mint is initialized.</summary>
    public bool IsInitialized { get; }

    /// <summary>The freeze authority, or <c>null</c>.</summary>
    public PublicKey? FreezeAuthority { get; }

    /// <summary>Token-2022 extension values in TLV order; empty for classic mint data.</summary>
    public IReadOnlyList<Token2022ExtensionData> Extensions { get; }

    /// <summary>Decodes classic 82-byte mint data or the Token-2022 extended-mint envelope.</summary>
    /// <param name="data">Complete mint account data.</param>
    /// <returns>
    /// The decoded state, or <c>null</c> when the data is malformed or uses the 355-byte multisignature length.
    /// </returns>
    public static TokenMintState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < BaseLength || !TokenStateDecoder.TryReadCOptionPublicKey(data, out var mintAuthority))
            return null;
        var initializedByte = data[45];
        if (initializedByte > 1 || !TokenStateDecoder.TryReadCOptionPublicKey(data[46..], out var freezeAuthority))
            return null;
        if (!TokenStateDecoder.TryDecodeExtensions(data, BaseLength, expectedAccountType: 1, out var extensions))
            return null;

        return new TokenMintState(
            mintAuthority,
            BinaryPrimitives.ReadUInt64LittleEndian(data[36..]),
            data[44],
            initializedByte != 0,
            freezeAuthority,
            extensions);
    }
}

/// <summary>A decoded classic SPL Token holding-account base state plus optional Token-2022 TLV extensions.</summary>
public sealed class TokenHoldingAccountState
{
    /// <summary>The classic token-account base-state length.</summary>
    public const int BaseLength = 165;

    internal TokenHoldingAccountState(
        PublicKey mint,
        PublicKey owner,
        ulong amount,
        PublicKey? @delegate,
        TokenAccountStatus status,
        ulong? nativeRentExemptReserve,
        ulong delegatedAmount,
        PublicKey? closeAuthority,
        IReadOnlyList<Token2022ExtensionData> extensions)
    {
        Mint = mint;
        Owner = owner;
        Amount = amount;
        Delegate = @delegate;
        Status = status;
        NativeRentExemptReserve = nativeRentExemptReserve;
        DelegatedAmount = delegatedAmount;
        CloseAuthority = closeAuthority;
        Extensions = extensions;
    }

    /// <summary>The associated mint.</summary>
    public PublicKey Mint { get; }

    /// <summary>The account owner.</summary>
    public PublicKey Owner { get; }

    /// <summary>The raw token balance.</summary>
    public ulong Amount { get; }

    /// <summary>The approved delegate, or <c>null</c>.</summary>
    public PublicKey? Delegate { get; }

    /// <summary>The account status.</summary>
    public TokenAccountStatus Status { get; }

    /// <summary>The wrapped-native rent-exempt reserve, or <c>null</c> for a non-native account.</summary>
    public ulong? NativeRentExemptReserve { get; }

    /// <summary>The remaining amount delegated to <see cref="Delegate"/>.</summary>
    public ulong DelegatedAmount { get; }

    /// <summary>The close authority, or <c>null</c>.</summary>
    public PublicKey? CloseAuthority { get; }

    /// <summary>Token-2022 extension values in TLV order; empty for classic account data.</summary>
    public IReadOnlyList<Token2022ExtensionData> Extensions { get; }

    /// <summary>Decodes classic 165-byte token-account data or the Token-2022 extended-account envelope.</summary>
    /// <param name="data">Complete token account data.</param>
    /// <returns>
    /// The decoded state, or <c>null</c> when the data is malformed or uses the 355-byte multisignature length.
    /// </returns>
    public static TokenHoldingAccountState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < BaseLength || !TokenStateDecoder.TryReadCOptionPublicKey(data[72..], out var @delegate))
            return null;
        var statusByte = data[108];
        if (statusByte > (byte)TokenAccountStatus.Frozen ||
            !TokenStateDecoder.TryReadCOptionUInt64(data[109..], out var nativeReserve) ||
            !TokenStateDecoder.TryReadCOptionPublicKey(data[129..], out var closeAuthority) ||
            !TokenStateDecoder.TryDecodeExtensions(data, BaseLength, expectedAccountType: 2, out var extensions))
        {
            return null;
        }

        return new TokenHoldingAccountState(
            new PublicKey(data[..PublicKey.Length]),
            new PublicKey(data.Slice(PublicKey.Length, PublicKey.Length)),
            BinaryPrimitives.ReadUInt64LittleEndian(data[64..]),
            @delegate,
            (TokenAccountStatus)statusByte,
            nativeReserve,
            BinaryPrimitives.ReadUInt64LittleEndian(data[121..]),
            closeAuthority,
            extensions);
    }
}

/// <summary>A decoded classic SPL Token multisignature account.</summary>
public sealed class TokenMultisigState
{
    /// <summary>The fixed multisig account length.</summary>
    public const int Length = 355;

    internal TokenMultisigState(byte requiredSignatures, byte signerCount, bool isInitialized, PublicKey[] signers)
    {
        RequiredSignatures = requiredSignatures;
        SignerCount = signerCount;
        IsInitialized = isInitialized;
        Signers = signers;
    }

    /// <summary>The number of required member signatures.</summary>
    public byte RequiredSignatures { get; }

    /// <summary>The number of configured signer slots.</summary>
    public byte SignerCount { get; }

    /// <summary>Whether the multisig is initialized.</summary>
    public bool IsInitialized { get; }

    /// <summary>All eleven encoded signer slots, matching the upstream state layout.</summary>
    public IReadOnlyList<PublicKey> Signers { get; }

    /// <summary>Decodes an exact 355-byte classic or Token-2022 multisig state.</summary>
    /// <param name="data">Complete multisig data.</param>
    /// <returns>The decoded state, or <c>null</c> when the length or boolean is invalid.</returns>
    public static TokenMultisigState? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length || data[2] > 1)
            return null;
        var signers = new PublicKey[11];
        for (var i = 0; i < signers.Length; i++)
            signers[i] = new PublicKey(data.Slice(3 + (i * PublicKey.Length), PublicKey.Length));
        return new TokenMultisigState(data[0], data[1], data[2] != 0, signers);
    }
}

internal static class TokenStateDecoder
{
    private const int ExtendedBaseLength = TokenHoldingAccountState.BaseLength;

    public static bool TryReadCOptionPublicKey(ReadOnlySpan<byte> data, out PublicKey? publicKey)
    {
        if (data.Length < sizeof(uint) + PublicKey.Length)
        {
            publicKey = null;
            return false;
        }

        var tag = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (tag == 0)
        {
            publicKey = null;
            return true;
        }

        if (tag == 1)
        {
            publicKey = new PublicKey(data.Slice(sizeof(uint), PublicKey.Length));
            return true;
        }

        publicKey = null;
        return false;
    }

    public static bool TryReadCOptionUInt64(ReadOnlySpan<byte> data, out ulong? value)
    {
        if (data.Length < sizeof(uint) + sizeof(ulong))
        {
            value = null;
            return false;
        }

        var tag = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (tag == 0)
        {
            value = null;
            return true;
        }

        if (tag == 1)
        {
            value = BinaryPrimitives.ReadUInt64LittleEndian(data[sizeof(uint)..]);
            return true;
        }

        value = null;
        return false;
    }

    public static bool TryDecodeExtensions(
        ReadOnlySpan<byte> data,
        int baseLength,
        byte expectedAccountType,
        out IReadOnlyList<Token2022ExtensionData> extensions)
    {
        if (data.Length == baseLength)
        {
            extensions = [];
            return true;
        }

        var paddingLength = ExtendedBaseLength - baseLength;
        if (data.Length == TokenMultisigState.Length ||
            data.Length <= ExtendedBaseLength ||
            data.Slice(baseLength, paddingLength).IndexOfAnyExcept((byte)0) >= 0 ||
            data[ExtendedBaseLength] != expectedAccountType)
        {
            extensions = [];
            return false;
        }

        var decoded = new List<Token2022ExtensionData>();
        var tlv = data[(ExtendedBaseLength + 1)..];
        var offset = 0;
        while (offset < tlv.Length)
        {
            if (tlv.Length - offset < sizeof(ushort))
                break;
            var typeValue = BinaryPrimitives.ReadUInt16LittleEndian(tlv[offset..]);
            if (typeValue == 0)
                break;
            if (typeValue > (ushort)Token2022ExtensionType.PermissionedBurn ||
                tlv.Length - offset < 2 * sizeof(ushort))
            {
                extensions = [];
                return false;
            }

            var valueLength = BinaryPrimitives.ReadUInt16LittleEndian(tlv[(offset + sizeof(ushort))..]);
            var valueStart = offset + (2 * sizeof(ushort));
            if (valueLength > tlv.Length - valueStart)
            {
                extensions = [];
                return false;
            }

            decoded.Add(new Token2022ExtensionData(
                (Token2022ExtensionType)typeValue,
                tlv.Slice(valueStart, valueLength)));
            offset = valueStart + valueLength;
        }

        extensions = decoded;
        return true;
    }
}
