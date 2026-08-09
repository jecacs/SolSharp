using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Rpc.Models;

/// <summary>The lookup lifecycle state that can be established from ALT metadata and observation context.</summary>
public enum AddressLookupTableLifecycle
{
    /// <summary>No deactivation has been requested.</summary>
    Activated,

    /// <summary>The table is in its SlotHashes cooldown and remains usable for address lookups.</summary>
    Deactivating,

    /// <summary>
    /// Deactivation was requested, but the account response does not include SlotHashes and therefore cannot
    /// distinguish a table still cooling down from one that is fully deactivated.
    /// </summary>
    DeactivationStatusUnknown
}

/// <summary>
/// A decoded on-chain Address Lookup Table account: its metadata plus its stored and context-visible addresses. Feed
/// <see cref="Addresses"/> into a v0 transaction (an <c>AddressLookupTableAccount</c> in SolSharp.Programs)
/// to load those accounts without listing them in the message.
/// </summary>
/// <seealso href="https://solana.com/docs/rpc/http/getaccountinfo">getAccountInfo</seealso>
public sealed record AddressLookupTable
{
    private const int MetaSize = 56;
    private const int MaxAddresses = 256;
    private const ulong SlotHashesCapacity = 512;

    /// <summary>The slot at which deactivation began, or <see cref="ulong.MaxValue"/> when it has not begun.</summary>
    public required ulong DeactivationSlot { get; init; }

    /// <summary>The most recent slot in which the table was extended.</summary>
    public required ulong LastExtendedSlot { get; init; }

    /// <summary>The first address index appended during <see cref="LastExtendedSlot"/>.</summary>
    public byte LastExtendedSlotStartIndex { get; init; }

    /// <summary>The authority allowed to extend or close the table, or <c>null</c> if it has been frozen.</summary>
    public required PublicKey? Authority { get; init; }

    /// <summary>The serialized metadata padding retained from the upstream layout.</summary>
    public ushort Padding { get; init; }

    /// <summary>The RPC context slot used to determine address visibility, when the table came from the client.</summary>
    public ulong? ContextSlot { get; init; }

    /// <summary>The complete serialized address list, including addresses not usable during their extension slot.</summary>
    public IReadOnlyList<PublicKey> StoredAddresses { get; init; } = [];

    /// <summary>
    /// The addresses usable at <see cref="ContextSlot"/>, in index order. Addresses appended in the table's
    /// extension slot are intentionally excluded, matching Agave transaction lookup semantics.
    /// </summary>
    public required IReadOnlyList<PublicKey> Addresses { get; init; }

    /// <summary>
    /// Whether deactivation has not begun. A <c>false</c> value means deactivation was requested, not that the
    /// table is already unusable; inspect <see cref="Lifecycle"/> and <see cref="IsUsable"/> for that distinction.
    /// </summary>
    public bool IsActive => DeactivationSlot == ulong.MaxValue;

    /// <summary>
    /// The lifecycle state that can be established without SlotHashes. A finite old deactivation slot becomes
    /// <see cref="AddressLookupTableLifecycle.DeactivationStatusUnknown"/> once usability cannot be proven.
    /// </summary>
    public AddressLookupTableLifecycle Lifecycle => GetLifecycleWithoutSlotHashes();

    /// <summary>
    /// Whether the table is known to be usable at <see cref="ContextSlot"/>. <c>null</c> means exact usability
    /// requires the SlotHashes sysvar; it never guesses that a cooling-down table is inactive.
    /// </summary>
    public bool? IsUsable => Lifecycle switch
    {
        AddressLookupTableLifecycle.Activated or AddressLookupTableLifecycle.Deactivating => true,
        _ => null
    };

    /// <summary>Decodes a lookup table from its raw account data (the bytes <c>getAccountInfo</c> returns).</summary>
    /// <param name="data">The account's raw data.</param>
    /// <returns>The decoded table, or <c>null</c> if the data is not an initialized lookup table.</returns>
    public static AddressLookupTable? Decode(ReadOnlySpan<byte> data) => Decode(data, contextSlot: null);

    /// <summary>
    /// Decodes a lookup table and exposes only addresses active at the supplied RPC context slot.
    /// </summary>
    /// <param name="data">The account's raw data.</param>
    /// <param name="contextSlot">The bank slot whose account state was returned.</param>
    /// <returns>The decoded table, or <c>null</c> if the data or metadata bounds are invalid.</returns>
    public static AddressLookupTable? Decode(ReadOnlySpan<byte> data, ulong contextSlot) => Decode(data, (ulong?)contextSlot);

    private static AddressLookupTable? Decode(ReadOnlySpan<byte> data, ulong? contextSlot)
    {
        // Layout: u32 discriminant (1 = LookupTable), u64 deactivation slot, u64 last-extended slot, u8 start
        // index, Option<Pubkey> authority (1-byte flag + 32-byte key), u16 padding = 56 bytes, then a tightly
        // packed array of 32-byte addresses.
        if (data.Length < MetaSize
            || (data.Length - MetaSize) % PublicKey.Length != 0
            || (data.Length - MetaSize) / PublicKey.Length > MaxAddresses
            || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1)
            return null;

        var deactivationSlot = BinaryPrimitives.ReadUInt64LittleEndian(data[4..]);
        var lastExtendedSlot = BinaryPrimitives.ReadUInt64LittleEndian(data[12..]);
        var lastExtendedSlotStartIndex = data[20];
        PublicKey? authority;
        ushort padding;
        switch (data[21])
        {
            case 0:
                authority = null;
                padding = BinaryPrimitives.ReadUInt16LittleEndian(data[22..]);
                break;
            case 1:
                authority = new PublicKey(data.Slice(22, PublicKey.Length));
                padding = BinaryPrimitives.ReadUInt16LittleEndian(data[54..]);
                break;
            default:
                return null;
        }

        var addressBytes = data[MetaSize..];
        var count = addressBytes.Length / PublicKey.Length;
        if (lastExtendedSlotStartIndex > count)
            return null;

        var addresses = new PublicKey[count];
        for (var i = 0; i < count; i++)
            addresses[i] = new PublicKey(addressBytes.Slice(i * PublicKey.Length, PublicKey.Length));

        IReadOnlyList<PublicKey> activeAddresses = addresses;
        if (contextSlot is { } observedSlot && observedSlot <= lastExtendedSlot)
        {
            var activePrefix = new PublicKey[lastExtendedSlotStartIndex];
            Array.Copy(addresses, activePrefix, activePrefix.Length);
            activeAddresses = activePrefix;
        }

        return new AddressLookupTable
        {
            DeactivationSlot = deactivationSlot,
            LastExtendedSlot = lastExtendedSlot,
            LastExtendedSlotStartIndex = lastExtendedSlotStartIndex,
            Authority = authority,
            Padding = padding,
            ContextSlot = contextSlot,
            StoredAddresses = addresses,
            Addresses = activeAddresses
        };
    }

    private AddressLookupTableLifecycle GetLifecycleWithoutSlotHashes()
    {
        if (DeactivationSlot == ulong.MaxValue)
            return AddressLookupTableLifecycle.Activated;

        if (ContextSlot is not { } contextSlot)
            return AddressLookupTableLifecycle.DeactivationStatusUnknown;

        if (contextSlot == DeactivationSlot ||
            (contextSlot > DeactivationSlot && contextSlot - DeactivationSlot <= SlotHashesCapacity))
        {
            return AddressLookupTableLifecycle.Deactivating;
        }

        return AddressLookupTableLifecycle.DeactivationStatusUnknown;
    }
}
