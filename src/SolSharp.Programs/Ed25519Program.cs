using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

/// <summary>Builds and decodes Ed25519 native signature-verification instructions.</summary>
public static class Ed25519Program
{
    /// <summary>The serialized Ed25519 public-key length.</summary>
    public const int PublicKeyLength = 32;

    /// <summary>The serialized Ed25519 signature length.</summary>
    public const int SignatureLength = 64;

    /// <summary>The serialized length of one offsets record.</summary>
    public const int SignatureOffsetsLength = 14;

    /// <summary>The first byte of self-contained signature data.</summary>
    public const int DataStart = 16;

    /// <summary>The Ed25519 native precompile address.</summary>
    public static readonly PublicKey ProgramId =
        PublicKey.Parse("Ed25519SigVerify111111111111111111111111111");

    /// <summary>Builds a self-contained verification instruction for one precomputed signature.</summary>
    /// <param name="message">The signed message.</param>
    /// <param name="signature">The 64-byte Ed25519 signature.</param>
    /// <param name="publicKey">The 32-byte Ed25519 public key.</param>
    /// <returns>The account-free precompile instruction.</returns>
    public static Instruction CreateInstruction(
        ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature,
        ReadOnlySpan<byte> publicKey)
    {
        ValidateLength(signature, SignatureLength, nameof(signature));
        ValidateLength(publicKey, PublicKeyLength, nameof(publicKey));
        ValidateMessageLength(message);

        const ushort publicKeyOffset = DataStart;
        const ushort signatureOffset = DataStart + PublicKeyLength;
        const ushort messageOffset = DataStart + PublicKeyLength + SignatureLength;
        var data = new byte[messageOffset + message.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data, 1);
        WriteOffsets(
            data.AsSpan(2),
            new Ed25519SignatureOffsets(
                signatureOffset,
                ushort.MaxValue,
                publicKeyOffset,
                ushort.MaxValue,
                messageOffset,
                (ushort)message.Length,
                ushort.MaxValue));
        publicKey.CopyTo(data.AsSpan(publicKeyOffset, PublicKeyLength));
        signature.CopyTo(data.AsSpan(signatureOffset, SignatureLength));
        message.CopyTo(data.AsSpan(messageOffset));
        return new Instruction { ProgramId = ProgramId, Accounts = [], Data = data };
    }

    /// <summary>Builds an offsets-only instruction for data stored in this or other instructions.</summary>
    /// <param name="offsets">The offsets records.</param>
    /// <returns>The account-free precompile instruction.</returns>
    /// <exception cref="ArgumentException">More than 255 records are supplied.</exception>
    public static Instruction CreateOffsetsInstruction(IReadOnlyList<Ed25519SignatureOffsets> offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);
        if (offsets.Count > byte.MaxValue)
            throw new ArgumentException("The Ed25519 precompile accepts at most 255 offset records.", nameof(offsets));

        var data = new byte[checked(2 + (offsets.Count * SignatureOffsetsLength))];
        data[0] = (byte)offsets.Count;
        for (var i = 0; i < offsets.Count; i++)
            WriteOffsets(data.AsSpan(2 + (i * SignatureOffsetsLength)), offsets[i]);
        return new Instruction { ProgramId = ProgramId, Accounts = [], Data = data };
    }

    /// <summary>Decodes the offsets table at the start of Ed25519 instruction data.</summary>
    /// <param name="data">The complete instruction data.</param>
    /// <returns>The decoded records; appended signature data is ignored.</returns>
    /// <exception cref="ArgumentException">The header or offsets table is truncated.</exception>
    public static Ed25519SignatureOffsets[] DecodeOffsets(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            throw new ArgumentException("Ed25519 instruction data requires a two-byte count.", nameof(data));

        var count = data[0];
        if (count == 0 && data.Length > 2)
            throw new ArgumentException("A zero-count Ed25519 instruction cannot contain trailing data.", nameof(data));
        var tableLength = checked(2 + (count * SignatureOffsetsLength));
        if (data.Length < tableLength)
            throw new ArgumentException("Ed25519 instruction data contains a truncated offsets table.", nameof(data));

        var offsets = new Ed25519SignatureOffsets[count];
        for (var i = 0; i < offsets.Length; i++)
        {
            var record = data[(2 + (i * SignatureOffsetsLength))..];
            offsets[i] = new Ed25519SignatureOffsets(
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

    private static void WriteOffsets(Span<byte> destination, Ed25519SignatureOffsets offsets)
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
