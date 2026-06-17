using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Instruction builders for the Address Lookup Table program: create a table, extend it with addresses,
/// deactivate it, and close it. The addresses a table stores are later loaded by a <see cref="MessageV0"/>.
/// </summary>
public static class AddressLookupTableProgram
{
    /// <summary>The Address Lookup Table program's address.</summary>
    public static PublicKey ProgramId { get; } = PublicKey.Parse("AddressLookupTab1e1111111111111111111111111");

    /// <summary>
    /// Creates a new lookup table owned by <paramref name="authority"/>. The table's address is a PDA derived
    /// from the authority and <paramref name="recentSlot"/>, which must be a recent slot the node has seen.
    /// </summary>
    /// <param name="authority">The account that will control the table (signer).</param>
    /// <param name="payer">The account that funds the new table account (writable signer).</param>
    /// <param name="recentSlot">A recent slot; the table address is derived from it, so it must be current.</param>
    /// <returns>The create instruction and the derived lookup table address.</returns>
    public static (Instruction Instruction, PublicKey LookupTable) CreateLookupTable(PublicKey authority, PublicKey payer, ulong recentSlot)
    {
        Span<byte> slotBytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(slotBytes, recentSlot);
        var (lookupTable, bump) = ProgramDerivedAddress.FindProgramAddress([authority.ToBytes(), slotBytes.ToArray()], ProgramId);

        // Instruction 0 (CreateLookupTable): u32 discriminant, u64 recent slot, u8 bump seed.
        var data = new byte[sizeof(uint) + sizeof(ulong) + 1];
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(sizeof(uint)), recentSlot);
        data[^1] = bump;

        var instruction = new Instruction
        {
            ProgramId = ProgramId,
            Accounts =
            [
                AccountMeta.Writable(lookupTable),
                AccountMeta.ReadonlySigner(authority),
                AccountMeta.WritableSigner(payer),
                AccountMeta.Readonly(SystemProgram.ProgramId)
            ],
            Data = data
        };

        return (instruction, lookupTable);
    }

    /// <summary>Appends addresses to an existing lookup table.</summary>
    /// <param name="lookupTable">The table to extend (writable).</param>
    /// <param name="authority">The table's authority (signer).</param>
    /// <param name="payer">The account that funds any new rent; pass <c>null</c> if the table is already rent-exempt for the new size.</param>
    /// <param name="newAddresses">The addresses to append.</param>
    /// <returns>The extend instruction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="newAddresses"/> is <c>null</c>.</exception>
    public static Instruction ExtendLookupTable(PublicKey lookupTable, PublicKey authority, PublicKey? payer, IReadOnlyList<PublicKey> newAddresses)
    {
        ArgumentNullException.ThrowIfNull(newAddresses);

        using var buffer = new MemoryStream(sizeof(uint) + sizeof(ulong) + newAddresses.Count * PublicKey.Length);
        Span<byte> head = stackalloc byte[sizeof(uint) + sizeof(ulong)];
        BinaryPrimitives.WriteUInt32LittleEndian(head, 2);
        BinaryPrimitives.WriteUInt64LittleEndian(head[sizeof(uint)..], (ulong)newAddresses.Count);
        buffer.Write(head);
        foreach (var address in newAddresses)
            buffer.Write(address.ToBytes());

        var accounts = new List<AccountMeta>(4)
        {
            AccountMeta.Writable(lookupTable),
            AccountMeta.ReadonlySigner(authority)
        };
        if (payer is { } payerKey)
        {
            accounts.Add(AccountMeta.WritableSigner(payerKey));
            accounts.Add(AccountMeta.Readonly(SystemProgram.ProgramId));
        }

        return new Instruction
        {
            ProgramId = ProgramId,
            Accounts = accounts,
            Data = buffer.ToArray()
        };
    }

    /// <summary>Deactivates a lookup table, starting the cool-down after which it can be closed.</summary>
    /// <param name="lookupTable">The table to deactivate (writable).</param>
    /// <param name="authority">The table's authority (signer).</param>
    /// <returns>The deactivate instruction.</returns>
    public static Instruction DeactivateLookupTable(PublicKey lookupTable, PublicKey authority)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(lookupTable), AccountMeta.ReadonlySigner(authority)],
            Data = Discriminator(3)
        };

    /// <summary>Closes a deactivated lookup table and refunds its lamports to <paramref name="recipient"/>.</summary>
    /// <param name="lookupTable">The deactivated table to close (writable).</param>
    /// <param name="authority">The table's authority (signer).</param>
    /// <param name="recipient">The account that receives the reclaimed lamports (writable).</param>
    /// <returns>The close instruction.</returns>
    public static Instruction CloseLookupTable(PublicKey lookupTable, PublicKey authority, PublicKey recipient)
        => new()
        {
            ProgramId = ProgramId,
            Accounts = [AccountMeta.Writable(lookupTable), AccountMeta.ReadonlySigner(authority), AccountMeta.Writable(recipient)],
            Data = Discriminator(4)
        };

    private static byte[] Discriminator(uint instruction)
    {
        var data = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(data, instruction);
        return data;
    }
}
