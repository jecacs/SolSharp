using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>
/// A decoded on-chain Address Lookup Table account: its metadata plus the addresses it stores. Feed
/// <see cref="Addresses"/> into a v0 transaction (an <c>AddressLookupTableAccount</c> in SolSharp.Programs)
/// to load those accounts without listing them in the message.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
public sealed record AddressLookupTable
{
    private const int MetaSize = 56;

    /// <summary>The slot the table was deactivated at, or <see cref="ulong.MaxValue"/> while it is still active.</summary>
    public required ulong DeactivationSlot { get; init; }

    /// <summary>The most recent slot in which the table was extended.</summary>
    public required ulong LastExtendedSlot { get; init; }

    /// <summary>The authority allowed to extend or close the table, or <c>null</c> if it has been frozen.</summary>
    public required PublicKey? Authority { get; init; }

    /// <summary>The addresses the table stores, in index order.</summary>
    public required IReadOnlyList<PublicKey> Addresses { get; init; }

    /// <summary>True while the table is active (not deactivated) and so usable in new transactions.</summary>
    public bool IsActive => DeactivationSlot == ulong.MaxValue;

    /// <summary>Decodes a lookup table from its raw account data (the bytes <c>getAccountInfo</c> returns).</summary>
    /// <param name="data">The account's raw data.</param>
    /// <returns>The decoded table, or <c>null</c> if the data is not an initialized lookup table.</returns>
    public static AddressLookupTable? Decode(ReadOnlySpan<byte> data)
    {
        // Layout: u32 discriminant (1 = LookupTable), u64 deactivation slot, u64 last-extended slot, u8 start
        // index, Option<Pubkey> authority (1-byte flag + 32-byte key), u16 padding = 56 bytes, then a tightly
        // packed array of 32-byte addresses.
        if (data.Length < MetaSize || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1)
            return null;

        var deactivationSlot = BinaryPrimitives.ReadUInt64LittleEndian(data[4..]);
        var lastExtendedSlot = BinaryPrimitives.ReadUInt64LittleEndian(data[12..]);
        PublicKey? authority = data[21] != 0 ? new PublicKey(data.Slice(22, PublicKey.Length)) : null;

        var addressBytes = data[MetaSize..];
        var count = addressBytes.Length / PublicKey.Length;
        var addresses = new PublicKey[count];
        for (var i = 0; i < count; i++)
            addresses[i] = new PublicKey(addressBytes.Slice(i * PublicKey.Length, PublicKey.Length));

        return new AddressLookupTable
        {
            DeactivationSlot = deactivationSlot,
            LastExtendedSlot = lastExtendedSlot,
            Authority = authority,
            Addresses = addresses
        };
    }
}
