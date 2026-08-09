using System.Buffers.Binary;
using System.Text;
using SolSharp.Core.Primitives;

namespace SolSharp.Programs;

internal static class ProgramWireEncoding
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static byte[] Build(uint discriminator, Action<MemoryStream>? writePayload = null)
    {
        using var stream = new MemoryStream();
        WriteUInt32(stream, discriminator);
        writePayload?.Invoke(stream);
        return stream.ToArray();
    }

    public static void WriteByte(MemoryStream stream, byte value) => stream.WriteByte(value);

    public static void WriteUInt16(MemoryStream stream, ushort value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    public static void WriteUInt32(MemoryStream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    public static void WriteUInt64(MemoryStream stream, ulong value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    public static void WriteInt64(MemoryStream stream, long value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value);
        stream.Write(bytes);
    }

    public static void WritePublicKey(MemoryStream stream, PublicKey publicKey)
    {
        Span<byte> bytes = stackalloc byte[PublicKey.Length];
        publicKey.CopyTo(bytes);
        stream.Write(bytes);
    }

    public static void WriteHash(MemoryStream stream, Hash hash)
    {
        Span<byte> bytes = stackalloc byte[Hash.Length];
        hash.CopyTo(bytes);
        stream.Write(bytes);
    }

    public static void WriteByteVector(MemoryStream stream, ReadOnlySpan<byte> bytes)
    {
        WriteUInt64(stream, checked((ulong)bytes.Length));
        stream.Write(bytes);
    }

    public static void WriteString(MemoryStream stream, string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        byte[] bytes;
        try
        {
            bytes = StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException("The value must contain valid Unicode text.", parameterName, exception);
        }

        WriteUInt64(stream, checked((ulong)bytes.Length));
        stream.Write(bytes);
    }

    public static void WriteOptionalInt64(MemoryStream stream, long? value)
    {
        WriteByte(stream, value.HasValue ? (byte)1 : (byte)0);
        if (value.HasValue)
            WriteInt64(stream, value.Value);
    }

    public static void WriteOptionalUInt64(MemoryStream stream, ulong? value)
    {
        WriteByte(stream, value.HasValue ? (byte)1 : (byte)0);
        if (value.HasValue)
            WriteUInt64(stream, value.Value);
    }

    public static void WriteOptionalPublicKey(MemoryStream stream, PublicKey? value)
    {
        WriteByte(stream, value.HasValue ? (byte)1 : (byte)0);
        if (value.HasValue)
            WritePublicKey(stream, value.Value);
    }

    public static void WriteShortVectorLength(MemoryStream stream, int length)
    {
        if (length is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(length), length, "A Solana short vector may contain at most 65,535 elements.");

        var remaining = (uint)length;
        do
        {
            var next = (byte)(remaining & 0x7f);
            remaining >>= 7;
            if (remaining is not 0)
                next |= 0x80;
            stream.WriteByte(next);
        }
        while (remaining is not 0);
    }

    public static void WriteUnsignedLeb128(MemoryStream stream, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7f);
            value >>= 7;
            if (value != 0)
                next |= 0x80;
            stream.WriteByte(next);
        }
        while (value != 0);
    }
}
