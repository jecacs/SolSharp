using System.Buffers.Binary;
using SolSharp.Core.Encoding;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// The decoded state of an initialized durable nonce account (the 80-byte bincode layout the System
/// program stores: a u32 version tag, a u32 state tag, the authority, the durable nonce, and the fee
/// calculator). <see cref="Nonce"/> is the value a nonce-anchored transaction uses as its recent blockhash.
/// </summary>
/// <seealso href="https://solana.com/developers/guides/advanced/introduction-to-durable-nonces">Durable nonces</seealso>
public sealed record NonceAccount
{
    /// <summary>The serialized size of a nonce account, in bytes (80).</summary>
    public const int Length = 80;

    /// <summary>The nonce state version tag (0 = legacy, 1 = current).</summary>
    public required uint Version { get; init; }

    /// <summary>The authority allowed to advance the nonce and withdraw the account's lamports.</summary>
    public required PublicKey Authority { get; init; }

    /// <summary>The current durable nonce (base58), used as the recent blockhash of a nonce-anchored transaction.</summary>
    public required string Nonce { get; init; }

    /// <summary>The fee per signature (in lamports) captured when the nonce was last advanced.</summary>
    public required ulong LamportsPerSignature { get; init; }

    /// <summary>Decodes a nonce account from its raw account data (the bytes <c>getAccountInfo</c> returns).</summary>
    /// <param name="data">The account's raw data.</param>
    /// <returns>
    /// The decoded nonce account, or <c>null</c> if the data is too short or the account is not an
    /// initialized nonce (its state tag is not 1).
    /// </returns>
    public static NonceAccount? Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < Length)
            return null;

        // bincode enum tags: Versions (0 = Legacy, 1 = Current), then State (0 = Uninitialized, 1 = Initialized).
        var state = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (state != 1)
            return null;

        return new NonceAccount
        {
            Version = BinaryPrimitives.ReadUInt32LittleEndian(data),
            Authority = new PublicKey(data.Slice(8, PublicKey.Length)),
            Nonce = Base58.Encode(data.Slice(40, 32)),
            LamportsPerSignature = BinaryPrimitives.ReadUInt64LittleEndian(data[72..])
        };
    }
}
