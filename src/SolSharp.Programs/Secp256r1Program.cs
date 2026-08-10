using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds and decodes Secp256r1 native signature-verification instructions.</summary>
public static class Secp256r1Program
{
    /// <summary>The compressed Secp256r1 public-key length.</summary>
    public const int CompressedPublicKeyLength = 33;

    /// <summary>The compact Secp256r1 signature length.</summary>
    public const int SignatureLength = 64;

    /// <summary>The serialized length of one offsets record.</summary>
    public const int SignatureOffsetsLength = 14;

    /// <summary>The start offset for a self-contained instruction's payload.</summary>
    public const int DataStart = 16;

    /// <summary>The Secp256r1 native precompile address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("Secp256r1SigVerify1111111111111111111111111");

    /// <summary>Builds a self-contained verification instruction for one precomputed signature.</summary>
    /// <param name="message">The signed message; the precompile hashes it with SHA-256.</param>
    /// <param name="signature">The 64-byte compact, low-S signature.</param>
    /// <param name="compressedPublicKey">The 33-byte compressed public key.</param>
    /// <returns>The account-free precompile instruction.</returns>
    public static Instruction CreateInstruction(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> compressedPublicKey)
    {
        ValidateLength(signature, SignatureLength, nameof(signature));
        ValidateLength(compressedPublicKey, CompressedPublicKeyLength, nameof(compressedPublicKey));
        ValidateMessageLength(message);

        const ushort publicKeyOffset = DataStart;
        const ushort signatureOffset = DataStart + CompressedPublicKeyLength;
        const ushort messageOffset = DataStart + CompressedPublicKeyLength + SignatureLength;
        var data = new byte[messageOffset + message.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data, 1);
        WriteOffsets(
            data.AsSpan(2),
            new(
                signatureOffset,
                ushort.MaxValue,
                publicKeyOffset,
                ushort.MaxValue,
                messageOffset,
                (ushort)message.Length,
                ushort.MaxValue));
        compressedPublicKey.CopyTo(data.AsSpan(publicKeyOffset, CompressedPublicKeyLength));
        signature.CopyTo(data.AsSpan(signatureOffset, SignatureLength));
        message.CopyTo(data.AsSpan(messageOffset));
        return new() { ProgramId = ProgramId, Accounts = [], Data = data };
    }

    /// <summary>Builds an offsets-only instruction for data stored in this or other instructions.</summary>
    /// <param name="offsets">The offsets records.</param>
    /// <returns>The account-free precompile instruction.</returns>
    /// <exception cref="ArgumentException">The record count is outside the runtime-supported range 1-8.</exception>
    public static Instruction CreateOffsetsInstruction(IReadOnlyList<Secp256r1SignatureOffsets> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        if (offsets.Count is < 1 or > 8)
            throw new ArgumentException("The Secp256r1 precompile accepts between 1 and 8 offset records.", nameof(offsets));

        var data = new byte[checked(2 + (offsets.Count * SignatureOffsetsLength))];
        data[0] = (byte)offsets.Count;
        for (var i = 0; i < offsets.Count; i++)
            WriteOffsets(data.AsSpan(2 + (i * SignatureOffsetsLength)), offsets[i]);

        return new()
        {
            ProgramId = ProgramId,
            Accounts = [],
            Data = data
        };
    }

    /// <summary>Decodes the offsets table at the start of Secp256r1 instruction data.</summary>
    /// <param name="data">The complete instruction data.</param>
    /// <returns>The decoded records; appended signature data is ignored.</returns>
    /// <exception cref="ArgumentException">The header or offsets table is truncated.</exception>
    public static Secp256r1SignatureOffsets[] DecodeOffsets(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            throw new ArgumentException("Secp256r1 instruction data requires a two-byte count.", nameof(data));

        var count = data[0];
        if (count is < 1 or > 8)
            throw new ArgumentException("A Secp256r1 instruction must contain between 1 and 8 offset records.", nameof(data));
        var tableLength = checked(2 + (count * SignatureOffsetsLength));
        if (data.Length < tableLength)
            throw new ArgumentException("Secp256r1 instruction data contains a truncated offsets table.", nameof(data));

        var offsets = new Secp256r1SignatureOffsets[count];
        for (var i = 0; i < offsets.Length; i++)
        {
            var record = data[(2 + (i * SignatureOffsetsLength))..];
            offsets[i] = new(
                ReadUInt16(record, 0),
                ReadUInt16(record, 2),
                ReadUInt16(record, 4),
                ReadUInt16(record, 6),
                ReadUInt16(record, 8),
                ReadUInt16(record, 10),
                ReadUInt16(record, 12));
        }

        return offsets;
    }

    private static void WriteOffsets(Span<byte> destination, Secp256r1SignatureOffsets offsets)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination, offsets.SignatureOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[2..], offsets.SignatureInstructionIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], offsets.PublicKeyOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], offsets.PublicKeyInstructionIndex);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], offsets.MessageOffset);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], offsets.MessageLength);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[12..], offsets.MessageInstructionIndex);
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
