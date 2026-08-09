using System.Buffers.Binary;
using SolSharp.Core.Primitives;
using SolSharp.Rpc.Models.Token2022;

namespace SolSharp.Rpc.Models;

/// <summary>The decoded state of an SPL Token mint account (the fixed 82-byte SPL "Pack" layout).</summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
public sealed record Mint
{
    /// <summary>The serialized size of a mint account, in bytes.</summary>
    public const int Length = 82;

    /// <summary>The authority allowed to mint new tokens, or <c>null</c> if minting is permanently disabled.</summary>
    public required PublicKey? MintAuthority { get; init; }

    /// <summary>The total token supply, in base units.</summary>
    public required ulong Supply { get; init; }

    /// <summary>The number of base-unit decimal places.</summary>
    public required byte Decimals { get; init; }

    /// <summary>Whether the mint is initialized.</summary>
    public required bool IsInitialized { get; init; }

    /// <summary>The authority allowed to freeze token accounts of this mint, or <c>null</c> if freezing is disabled.</summary>
    public required PublicKey? FreezeAuthority { get; init; }

    /// <summary>Decodes a mint from its raw account data (the bytes <c>getAccountInfo</c> returns).</summary>
    /// <param name="data">The account's raw data.</param>
    /// <returns>The decoded mint, or <c>null</c> if the data is not a canonical Token or Token-2022 mint layout.</returns>
    public static Mint? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length != Length && TokenExtensionSet.DecodeMint(data) is null)
            return null;

        if (!SplLayout.TryReadCOptionPublicKey(data, 0, out var mintAuthority)
            || data[45] > 1
            || !SplLayout.TryReadCOptionPublicKey(data, 46, out var freezeAuthority))
            return null;

        return new Mint
        {
            MintAuthority = mintAuthority,
            Supply = BinaryPrimitives.ReadUInt64LittleEndian(data[36..]),
            Decimals = data[44],
            IsInitialized = data[45] == 1,
            FreezeAuthority = freezeAuthority
        };
    }
}
