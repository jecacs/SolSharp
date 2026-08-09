using System.Buffers.Binary;
using SolSharp.Core.Primitives;
using SolSharp.Core.SysvarStates;

namespace SolSharp.Programs;

/// <summary>The serialized Address Lookup Table program state variant.</summary>
public enum AddressLookupTableStateKind : uint
{
    /// <summary>The account is uninitialized.</summary>
    Uninitialized = 0,

    /// <summary>The account contains lookup-table metadata and addresses.</summary>
    LookupTable = 1
}

/// <summary>The runtime activation state of a lookup table at a particular slot.</summary>
public enum AddressLookupTableStatusKind
{
    /// <summary>The table has not begun deactivation.</summary>
    Activated,

    /// <summary>The table is cooling down and remains usable.</summary>
    Deactivating,

    /// <summary>The deactivation slot is no longer retained in SlotHashes.</summary>
    Deactivated
}

/// <summary>A lookup table's activation state and remaining cooldown blocks.</summary>
/// <param name="Kind">The activation-state branch.</param>
/// <param name="RemainingBlocks">
/// The conservative remaining cooldown count for <see cref="AddressLookupTableStatusKind.Deactivating"/>;
/// zero for the other branches.
/// </param>
public readonly record struct AddressLookupTableStatus(
    AddressLookupTableStatusKind Kind,
    int RemainingBlocks = 0);

/// <summary>Decoded Address Lookup Table metadata and stored addresses.</summary>
public sealed class AddressLookupTableState
{
    private readonly PublicKey[] _addresses;

    private AddressLookupTableState(
        AddressLookupTableStateKind kind,
        ulong deactivationSlot,
        ulong lastExtendedSlot,
        byte lastExtendedSlotStartIndex,
        PublicKey? authority,
        PublicKey[] addresses)
    {
        Kind = kind;
        DeactivationSlot = deactivationSlot;
        LastExtendedSlot = lastExtendedSlot;
        LastExtendedSlotStartIndex = lastExtendedSlotStartIndex;
        Authority = authority;
        _addresses = addresses;
        Addresses = Array.AsReadOnly(addresses);
    }

    /// <summary>The fixed metadata length before lookup addresses.</summary>
    public const int MetadataLength = 56;

    /// <summary>The maximum number of addresses in one table.</summary>
    public const int MaximumAddresses = 256;

    /// <summary>The decoded state variant.</summary>
    public AddressLookupTableStateKind Kind { get; }

    /// <summary>The deactivation slot, or <see cref="ulong.MaxValue"/> while activated.</summary>
    public ulong DeactivationSlot { get; }

    /// <summary>The slot in which the table was most recently extended.</summary>
    public ulong LastExtendedSlot { get; }

    /// <summary>The address index at which the most recent extension began.</summary>
    public byte LastExtendedSlotStartIndex { get; }

    /// <summary>The table authority, or <c>null</c> when the table is frozen or uninitialized.</summary>
    public PublicKey? Authority { get; }

    /// <summary>The stored lookup addresses.</summary>
    public IReadOnlyList<PublicKey> Addresses { get; }

    /// <summary>
    /// Estimates the last valid slot from the pinned SDK's 512-entry SlotHashes window. A current slot
    /// below the estimate is guaranteed to remain in cooldown; skipped blocks may keep the table usable
    /// at or beyond the estimate.
    /// </summary>
    /// <param name="deactivationSlot">The slot at which deactivation began.</param>
    /// <returns>The saturating deactivation slot plus the SlotHashes capacity.</returns>
    public static ulong EstimateLastValidSlot(ulong deactivationSlot)
        => deactivationSlot > ulong.MaxValue - SlotHashesSysvarState.MaximumEntries
            ? ulong.MaxValue
            : deactivationSlot + SlotHashesSysvarState.MaximumEntries;

    /// <summary>Computes the table's exact runtime activation state from the current SlotHashes state.</summary>
    /// <param name="currentSlot">The bank slot performing the lookup.</param>
    /// <param name="slotHashes">The current SlotHashes sysvar state.</param>
    /// <returns>The activated, cooling-down, or deactivated status.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slotHashes"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">This state is uninitialized.</exception>
    public AddressLookupTableStatus GetStatus(ulong currentSlot, SlotHashesSysvarState slotHashes)
    {
        ArgumentNullException.ThrowIfNull(slotHashes);
        EnsureInitialized();

        if (DeactivationSlot == ulong.MaxValue)
            return new AddressLookupTableStatus(AddressLookupTableStatusKind.Activated);
        if (DeactivationSlot == currentSlot)
        {
            return new AddressLookupTableStatus(
                AddressLookupTableStatusKind.Deactivating,
                SlotHashesSysvarState.MaximumEntries + 1);
        }

        for (var i = 0; i < slotHashes.Entries.Count; i++)
        {
            if (slotHashes.Entries[i].Slot == DeactivationSlot)
            {
                return new AddressLookupTableStatus(
                    AddressLookupTableStatusKind.Deactivating,
                    SlotHashesSysvarState.MaximumEntries - i);
            }
        }

        return new AddressLookupTableStatus(AddressLookupTableStatusKind.Deactivated);
    }

    /// <summary>Returns whether the table remains usable for lookups at the supplied slot.</summary>
    /// <param name="currentSlot">The bank slot performing the lookup.</param>
    /// <param name="slotHashes">The current SlotHashes sysvar state.</param>
    /// <returns><c>true</c> for activated and cooling-down tables; otherwise <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slotHashes"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">This state is uninitialized.</exception>
    public bool IsActive(ulong currentSlot, SlotHashesSysvarState slotHashes)
        => GetStatus(currentSlot, slotHashes).Kind is not AddressLookupTableStatusKind.Deactivated;

    /// <summary>Gets the number of addresses visible to a lookup in the supplied bank slot.</summary>
    /// <param name="currentSlot">The bank slot performing the lookup.</param>
    /// <param name="slotHashes">The current SlotHashes sysvar state.</param>
    /// <returns>The full stored count, or the pre-extension prefix for a same-slot lookup.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slotHashes"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The table is uninitialized or fully deactivated.</exception>
    public int GetActiveAddressesLength(ulong currentSlot, SlotHashesSysvarState slotHashes)
    {
        if (!IsActive(currentSlot, slotHashes))
            throw new InvalidOperationException("A deactivated lookup table is no longer available for address lookups.");

        return currentSlot > LastExtendedSlot ? _addresses.Length : LastExtendedSlotStartIndex;
    }

    /// <summary>Returns defensive copies of the addresses visible to a lookup in the supplied bank slot.</summary>
    /// <param name="currentSlot">The bank slot performing the lookup.</param>
    /// <param name="slotHashes">The current SlotHashes sysvar state.</param>
    /// <returns>The active address prefix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="slotHashes"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">The table is uninitialized or fully deactivated.</exception>
    /// <exception cref="FormatException">The stored same-slot start index exceeds the address count.</exception>
    public PublicKey[] GetActiveAddresses(ulong currentSlot, SlotHashesSysvarState slotHashes)
    {
        var count = GetActiveAddressesLength(currentSlot, slotHashes);
        if (count > _addresses.Length)
            throw new FormatException("The lookup table's active-address prefix exceeds its stored address count.");

        return [.. _addresses.AsSpan(0, count)];
    }

    /// <summary>Resolves lookup indexes against the active address prefix for the supplied bank slot.</summary>
    /// <param name="currentSlot">The bank slot performing the lookup.</param>
    /// <param name="indexes">The ordered one-byte address indexes.</param>
    /// <param name="slotHashes">The current SlotHashes sysvar state.</param>
    /// <returns>The resolved addresses in caller order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="indexes"/> or <paramref name="slotHashes"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The table is uninitialized or fully deactivated.</exception>
    /// <exception cref="FormatException">The active prefix or an index is invalid.</exception>
    public PublicKey[] Lookup(
        ulong currentSlot,
        IReadOnlyList<byte> indexes,
        SlotHashesSysvarState slotHashes)
    {
        ArgumentNullException.ThrowIfNull(indexes);
        var active = GetActiveAddresses(currentSlot, slotHashes);
        var resolved = new PublicKey[indexes.Count];
        for (var i = 0; i < indexes.Count; i++)
        {
            var index = indexes[i];
            if (index >= active.Length)
                throw new FormatException($"Lookup-table address index {index} is outside the active prefix.");
            resolved[i] = active[index];
        }

        return resolved;
    }

    /// <summary>Decodes complete Address Lookup Table account data.</summary>
    /// <param name="data">The account data.</param>
    /// <returns>The decoded state.</returns>
    /// <exception cref="ArgumentException">The data is truncated, misaligned, or contains too many addresses.</exception>
    /// <exception cref="FormatException">The state discriminator or authority option tag is invalid.</exception>
    public static AddressLookupTableState Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < sizeof(uint))
            throw new ArgumentException("Lookup-table state requires a four-byte discriminator.", nameof(data));

        var discriminator = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (discriminator == (uint)AddressLookupTableStateKind.Uninitialized)
        {
            return new AddressLookupTableState(
                AddressLookupTableStateKind.Uninitialized,
                ulong.MaxValue,
                0,
                0,
                null,
                []);
        }

        if (discriminator != (uint)AddressLookupTableStateKind.LookupTable)
            throw new FormatException($"Unknown lookup-table state discriminator {discriminator}.");
        if (data.Length < MetadataLength)
            throw new ArgumentException($"Initialized lookup-table data requires at least {MetadataLength} bytes.", nameof(data));

        var addressBytes = data[MetadataLength..];
        if (addressBytes.Length % PublicKey.Length is not 0)
            throw new ArgumentException("Lookup-table address data must be a multiple of 32 bytes.", nameof(data));

        var count = addressBytes.Length / PublicKey.Length;
        if (count > MaximumAddresses)
            throw new ArgumentException($"A lookup table may contain at most {MaximumAddresses} addresses.", nameof(data));

        var authority = data[21] switch
        {
            0 => (PublicKey?)null,
            1 => new PublicKey(data.Slice(22, PublicKey.Length)),
            var tag => throw new FormatException($"Invalid lookup-table authority option tag {tag}.")
        };
        var addresses = new PublicKey[count];
        for (var i = 0; i < addresses.Length; i++)
            addresses[i] = new PublicKey(addressBytes.Slice(i * PublicKey.Length, PublicKey.Length));

        return new AddressLookupTableState(
            AddressLookupTableStateKind.LookupTable,
            BinaryPrimitives.ReadUInt64LittleEndian(data[4..]),
            BinaryPrimitives.ReadUInt64LittleEndian(data[12..]),
            data[20],
            authority,
            addresses);
    }

    /// <summary>Attempts to decode Address Lookup Table account data.</summary>
    /// <param name="data">The account data.</param>
    /// <param name="state">The decoded state on success; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> when the input is a valid state.</returns>
    public static bool TryParse(ReadOnlySpan<byte> data, out AddressLookupTableState? state)
    {
        try
        {
            state = Parse(data);
            return true;
        }
        catch (ArgumentException)
        {
            state = null;
            return false;
        }
        catch (FormatException)
        {
            state = null;
            return false;
        }
    }

    private void EnsureInitialized()
    {
        if (Kind is not AddressLookupTableStateKind.LookupTable)
            throw new InvalidOperationException("An uninitialized lookup-table state has no activation status.");
    }
}
