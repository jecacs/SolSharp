using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds and decodes Ethereum-compatible Secp256k1 native verification instructions.</summary>
public static class Secp256k1Program
{
    /// <summary>The Ethereum address length.</summary>
    public const int EthereumAddressLength = 20;

    /// <summary>The compact Secp256k1 signature length, excluding recovery ID.</summary>
    public const int SignatureLength = 64;

    /// <summary>The serialized length of one offsets record.</summary>
    public const int SignatureOffsetsLength = 11;

    /// <summary>The start offset for a one-signature self-contained instruction's payload.</summary>
    public const int DataStart = 12;

    /// <summary>The Secp256k1 native precompile address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("KeccakSecp256k11111111111111111111111111111");

    /// <summary>Builds a self-contained verification instruction for one precomputed signature.</summary>
    /// <param name="message">The signed message; the precompile hashes it with Keccak-256.</param>
    /// <param name="signature">The 64-byte compact Secp256k1 signature.</param>
    /// <param name="recoveryId">The signature recovery ID.</param>
    /// <param name="ethereumAddress">The 20-byte Ethereum address.</param>
    /// <returns>The account-free precompile instruction.</returns>
    public static Instruction CreateInstruction(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature,
        byte recoveryId,
        ReadOnlySpan<byte> ethereumAddress)
    {
        ValidateLength(signature, SignatureLength, nameof(signature));
        ValidateLength(ethereumAddress, EthereumAddressLength, nameof(ethereumAddress));
        ValidateMessageLength(message);

        const ushort addressOffset = DataStart;
        const ushort signatureOffset = DataStart + EthereumAddressLength;
        const ushort messageOffset = DataStart + EthereumAddressLength + SignatureLength + 1;
        var data = new byte[messageOffset + message.Length];
        data[0] = 1;
        WriteOffsets(
            data.AsSpan(1),
            new Secp256k1SignatureOffsets(
                signatureOffset,
                0,
                addressOffset,
                0,
                messageOffset,
                (ushort)message.Length,
                0));
        ethereumAddress.CopyTo(data.AsSpan(addressOffset, EthereumAddressLength));
        signature.CopyTo(data.AsSpan(signatureOffset, SignatureLength));
        data[signatureOffset + SignatureLength] = recoveryId;
        message.CopyTo(data.AsSpan(messageOffset));
        return new Instruction { ProgramId = ProgramId, Accounts = [], Data = data };
    }

    /// <summary>Builds an offsets-only instruction for data stored in this or other instructions.</summary>
    /// <param name="offsets">The offsets records.</param>
    /// <returns>The account-free precompile instruction.</returns>
    public static Instruction CreateOffsetsInstruction(IReadOnlyList<Secp256k1SignatureOffsets> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        if (offsets.Count > byte.MaxValue)
            throw new ArgumentException("At most 255 offsets may be encoded.", nameof(offsets));

        var data = new byte[checked(1 + (offsets.Count * SignatureOffsetsLength))];
        data[0] = (byte)offsets.Count;
        for (var i = 0; i < offsets.Count; i++)
            WriteOffsets(data.AsSpan(1 + (i * SignatureOffsetsLength)), offsets[i]);
        return new Instruction { ProgramId = ProgramId, Accounts = [], Data = data };
    }

    /// <summary>Decodes the offsets table at the start of Secp256k1 instruction data.</summary>
    /// <param name="data">The complete instruction data.</param>
    /// <returns>The decoded records; appended signature data is ignored.</returns>
    /// <exception cref="ArgumentException">The header or offsets table is truncated.</exception>
    public static Secp256k1SignatureOffsets[] DecodeOffsets(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            throw new ArgumentException("Secp256k1 instruction data requires a count byte.", nameof(data));

        var count = data[0];
        if (count == 0 && data.Length > 1)
            throw new ArgumentException("A zero-count Secp256k1 instruction cannot contain trailing data.", nameof(data));
        var tableLength = 1 + (count * SignatureOffsetsLength);
        if (data.Length < tableLength)
            throw new ArgumentException("Secp256k1 instruction data contains a truncated offsets table.", nameof(data));

        var offsets = new Secp256k1SignatureOffsets[count];
        for (var i = 0; i < offsets.Length; i++)
        {
            var record = data[(1 + (i * SignatureOffsetsLength))..];
            offsets[i] = new Secp256k1SignatureOffsets(
                ReadUInt16(record, 0),
                record[2],
                ReadUInt16(record, 3),
                record[5],
                ReadUInt16(record, 6),
                ReadUInt16(record, 8),
                record[10]);
        }

        return offsets;
    }

    private static void WriteOffsets(Span<byte> destination, Secp256k1SignatureOffsets offsets)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, offsets.SignatureOffset);
        destination[2] = offsets.SignatureInstructionIndex;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[3..], offsets.EthereumAddressOffset);
        destination[5] = offsets.EthereumAddressInstructionIndex;
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], offsets.MessageOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], offsets.MessageLength);
        destination[10] = offsets.MessageInstructionIndex;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt16LittleEndian(data[offset..]);

    private static void ValidateLength(ReadOnlySpan<byte> value, int expected, string parameterName)
    {
        if (value.Length != expected)
            throw new ArgumentException($"Value must be exactly {expected} bytes, got {value.Length}.", parameterName);
    }

    private static void ValidateMessageLength(ReadOnlySpan<byte> message)
    {
        if (message.Length > ushort.MaxValue)
            throw new ArgumentException("A precompile message may contain at most 65,535 bytes.", nameof(message));
    }
}
