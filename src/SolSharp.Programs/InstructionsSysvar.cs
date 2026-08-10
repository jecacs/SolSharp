using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>
/// Constructs and decodes the native Instructions sysvar account layout used for transaction
/// instruction introspection.
/// </summary>
public static class InstructionsSysvar
{
    private const int HeaderValueLength = sizeof(ushort);
    private const int AccountMetaLength = 1 + PublicKey.Length;
    private const byte SignerFlag = 1;
    private const byte WritableFlag = 2;

    /// <summary>Constructs complete Instructions sysvar data, including the trailing current index.</summary>
    /// <param name="instructions">The transaction instructions in execution order.</param>
    /// <param name="currentInstructionIndex">The index exposed as the currently executing instruction.</param>
    /// <returns>The exact native sysvar account bytes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instructions"/> or one of its entries is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// A collection or instruction-data length exceeds its 16-bit wire field, or an instruction offset
    /// cannot be represented by the native layout.
    /// </exception>
    public static byte[] Serialize(
        IReadOnlyList<Instruction> instructions,
        ushort currentInstructionIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        if (instructions.Count > ushort.MaxValue)
            throw new ArgumentException("The Instructions sysvar can encode at most 65,535 instructions.", nameof(instructions));

        var headerLength = checked(HeaderValueLength + (instructions.Count * sizeof(ushort)));
        using var stream = new MemoryStream(headerLength + HeaderValueLength);
        stream.SetLength(headerLength);
        stream.Position = headerLength;
        WriteUInt16(stream.GetBuffer(), 0, (ushort)instructions.Count);

        for (var i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i]
                ?? throw new ArgumentNullException(nameof(instructions), $"Instruction at index {i} is null.");
            var accounts = instruction.Accounts
                ?? throw new ArgumentException($"Instruction {i} has null accounts.", nameof(instructions));
            var instructionData = instruction.Data
                ?? throw new ArgumentException($"Instruction {i} has null data.", nameof(instructions));
            if (accounts.Count > ushort.MaxValue)
                throw new ArgumentException($"Instruction {i} has more than 65,535 accounts.", nameof(instructions));
            if (instructionData.Length > ushort.MaxValue)
                throw new ArgumentException($"Instruction {i} data exceeds 65,535 bytes.", nameof(instructions));
            if (stream.Length > ushort.MaxValue)
                throw new ArgumentException($"Instruction {i} starts beyond the 16-bit sysvar offset range.", nameof(instructions));

            WriteUInt16(stream.GetBuffer(), HeaderValueLength + (i * sizeof(ushort)), (ushort)stream.Length);
            WriteUInt16(stream, (ushort)accounts.Count);
            foreach (var account in accounts)
            {
                var flags = (byte)0;
                if (account.IsSigner)
                    flags |= SignerFlag;
                if (account.IsWritable)
                    flags |= WritableFlag;
                stream.WriteByte(flags);
                WritePublicKey(stream, account.PublicKey);
            }

            WritePublicKey(stream, instruction.ProgramId);
            WriteUInt16(stream, (ushort)instructionData.Length);
            stream.Write(instructionData);
        }

        WriteUInt16(stream, currentInstructionIndex);
        return stream.ToArray();
    }

    /// <summary>Reads the number of instructions declared by sysvar data.</summary>
    /// <param name="data">The complete Instructions sysvar account data.</param>
    /// <returns>The declared instruction count.</returns>
    /// <exception cref="FormatException">The fixed header or offset table is truncated.</exception>
    public static ushort GetInstructionCount(ReadOnlySpan<byte> data)
    {
        EnsureAvailable(data, 0, HeaderValueLength, "instruction count");
        var count = BinaryPrimitives.ReadUInt16LittleEndian(data);
        EnsureAvailable(data, HeaderValueLength, count * sizeof(ushort), "instruction offset table");
        return count;
    }

    /// <summary>Reads the trailing current-instruction index.</summary>
    /// <param name="data">The complete Instructions sysvar account data.</param>
    /// <returns>The current instruction index.</returns>
    /// <exception cref="FormatException">The data is shorter than the two-byte index.</exception>
    public static ushort ReadCurrentInstructionIndex(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderValueLength)
            throw new FormatException("Instructions sysvar data is too short for its current index.");
        return BinaryPrimitives.ReadUInt16LittleEndian(data[^HeaderValueLength..]);
    }

    /// <summary>Writes the trailing current-instruction index in caller-owned sysvar data.</summary>
    /// <param name="data">The complete writable Instructions sysvar account data.</param>
    /// <param name="currentInstructionIndex">The index to store.</param>
    /// <exception cref="ArgumentException">The data is shorter than the two-byte index.</exception>
    public static void WriteCurrentInstructionIndex(Span<byte> data, ushort currentInstructionIndex)
    {
        if (data.Length < HeaderValueLength)
            throw new ArgumentException("Instructions sysvar data is too short for its current index.", nameof(data));
        BinaryPrimitives.WriteUInt16LittleEndian(data[^HeaderValueLength..], currentInstructionIndex);
    }

    /// <summary>Decodes the instruction at an absolute transaction index.</summary>
    /// <param name="data">The complete Instructions sysvar account data.</param>
    /// <param name="index">The zero-based instruction index.</param>
    /// <returns>The decoded instruction.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the declared table.</exception>
    /// <exception cref="FormatException">The table, instruction, account metadata, or data slice is malformed.</exception>
    public static Instruction ReadInstruction(ReadOnlySpan<byte> data, int index)
    {
        var count = GetInstructionCount(data);
        if ((uint)index >= count)
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Instruction index must be below {count}.");

        var offsetPosition = HeaderValueLength + (index * sizeof(ushort));
        var offset = BinaryPrimitives.ReadUInt16LittleEndian(data[offsetPosition..]);
        EnsureAvailable(data, offset, HeaderValueLength, "account count");
        var accountCount = BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);
        var cursor = offset + HeaderValueLength;
        var minimumLength = ((long)accountCount * AccountMetaLength) + PublicKey.Length + HeaderValueLength;
        if (minimumLength > data.Length - cursor)
            throw new FormatException("Instructions sysvar account metadata is truncated.");

        var accounts = new AccountMeta[accountCount];
        for (var i = 0; i < accounts.Length; i++)
        {
            var flags = data[cursor++];
            var key = new PublicKey(data.Slice(cursor, PublicKey.Length));
            cursor += PublicKey.Length;
            accounts[i] = new(
                key,
                isSigner: (flags & SignerFlag) != 0,
                isWritable: (flags & WritableFlag) != 0);
        }

        var programId = new PublicKey(data.Slice(cursor, PublicKey.Length));
        cursor += PublicKey.Length;
        var dataLength = BinaryPrimitives.ReadUInt16LittleEndian(data[cursor..]);
        cursor += HeaderValueLength;
        EnsureAvailable(data, cursor, dataLength, "instruction data");
        return new()
        {
            ProgramId = programId,
            Accounts = accounts,
            Data = [.. data.Slice(cursor, dataLength)]
        };
    }

    /// <summary>Decodes an instruction relative to the trailing current-instruction index.</summary>
    /// <param name="data">The complete Instructions sysvar account data.</param>
    /// <param name="relativeIndex">A signed offset from the current instruction.</param>
    /// <returns>The decoded relative instruction.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The resulting instruction index is negative or outside the table.</exception>
    /// <exception cref="FormatException">The sysvar data is malformed.</exception>
    public static Instruction ReadInstructionRelative(ReadOnlySpan<byte> data, int relativeIndex)
    {
        var index = (long)ReadCurrentInstructionIndex(data) + relativeIndex;
        if (index is < 0 or > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(relativeIndex), relativeIndex, "The relative instruction index is outside the table.");
        return ReadInstruction(data, (int)index);
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
        => BinaryPrimitives.WriteUInt16LittleEndian(destination[offset..], value);

    private static void WritePublicKey(Stream stream, PublicKey key)
    {
        Span<byte> bytes = stackalloc byte[PublicKey.Length];
        key.CopyTo(bytes);
        stream.Write(bytes);
    }

    private static void EnsureAvailable(ReadOnlySpan<byte> data, int offset, int length, string field)
    {
        if (offset < 0 || length < 0 || offset > data.Length - length)
            throw new FormatException($"Instructions sysvar data is truncated in its {field}.");
    }
}
