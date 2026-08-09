using System.Buffers.Binary;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models.Token2022;

namespace SolSharp.Rpc.Models;

/// <summary>The state of an SPL Token account.</summary>
public enum TokenAccountState : byte
{
    /// <summary>The account is not initialized.</summary>
    Uninitialized = 0,

    /// <summary>The account is initialized and usable.</summary>
    Initialized = 1,

    /// <summary>The account is frozen: it cannot transfer or be acted on until thawed.</summary>
    Frozen = 2
}

/// <summary>The decoded state of an SPL Token account (the fixed 165-byte SPL "Pack" layout).</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
public sealed record TokenAccount
{
    /// <summary>The serialized size of a token account, in bytes.</summary>
    public const int Length = 165;

    /// <summary>The mint this account holds tokens of.</summary>
    public required PublicKey Mint { get; init; }

    /// <summary>The account's owner.</summary>
    public required PublicKey Owner { get; init; }

    /// <summary>The token balance, in base units.</summary>
    public required ulong Amount { get; init; }

    /// <summary>The delegate authorized to transfer up to <see cref="DelegatedAmount"/>, or <c>null</c> if none.</summary>
    public required PublicKey? Delegate { get; init; }

    /// <summary>The account's state.</summary>
    public required TokenAccountState State { get; init; }

    /// <summary>The rent-exempt reserve when this is a native (wrapped SOL) account, or <c>null</c> otherwise.</summary>
    public required ulong? IsNative { get; init; }

    /// <summary>The amount the delegate is currently authorized to transfer.</summary>
    public required ulong DelegatedAmount { get; init; }

    /// <summary>The authority allowed to close the account, or <c>null</c> if none.</summary>
    public required PublicKey? CloseAuthority { get; init; }

    /// <summary>True when the account is frozen.</summary>
    public bool IsFrozen => State == TokenAccountState.Frozen;

    /// <summary>True when this is a native (wrapped SOL) account.</summary>
    public bool IsNativeAccount => IsNative is not null;

    /// <summary>Decodes a token account from its raw account data (the bytes <c>getAccountInfo</c> returns).</summary>
    /// <param name="data">The account's raw data.</param>
    /// <returns>The decoded account, or <c>null</c> if the data is not a canonical Token or Token-2022 account layout.</returns>
    public static TokenAccount? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length && TokenExtensionSet.DecodeAccount(data) is null)
            return null;

        if (!SplLayout.TryReadCOptionPublicKey(data, 72, out var delegateAuthority)
            || data[108] > (byte)TokenAccountState.Frozen
            || !SplLayout.TryReadCOptionU64(data, 109, out var isNative)
            || !SplLayout.TryReadCOptionPublicKey(data, 129, out var closeAuthority))
            return null;

        return new TokenAccount
        {
            Mint = new PublicKey(data[..PublicKey.Length]),
            Owner = new PublicKey(data.Slice(32, PublicKey.Length)),
            Amount = BinaryPrimitives.ReadUInt64LittleEndian(data[64..]),
            Delegate = delegateAuthority,
            State = (TokenAccountState)data[108],
            IsNative = isNative,
            DelegatedAmount = BinaryPrimitives.ReadUInt64LittleEndian(data[121..]),
            CloseAuthority = closeAuthority
        };
    }
}
