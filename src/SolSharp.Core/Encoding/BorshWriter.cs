using System.Buffers;
using System.Buffers.Binary;
using SolSharp.Core.Primitives;

namespace SolSharp.Core.Encoding;

/// <summary>
/// A growable writer for the Borsh binary encoding - the inverse of <see cref="BorshReader"/>. Writes
/// little-endian integers, bools, 32-byte public keys, fixed and length-prefixed byte sequences, UTF-8
/// strings, and Option / Vec prefixes, then hands back the bytes with <see cref="ToArray"/>.
/// </summary>
public sealed class BorshWriter
{
    private readonly ArrayBufferWriter<byte> _buffer;

    /// <summary>Creates an empty writer.</summary>
    public BorshWriter() => _buffer = new ArrayBufferWriter<byte>();

    /// <summary>Creates an empty writer with a preallocated capacity.</summary>
    /// <param name="initialCapacity">The number of bytes to reserve up front.</param>
    public BorshWriter(int initialCapacity) => _buffer = new ArrayBufferWriter<byte>(initialCapacity);

    /// <summary>The number of bytes written so far.</summary>
    public int Length => _buffer.WrittenCount;

    /// <summary>Writes a <see cref="byte"/> (u8).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteU8(byte value)
    {
        _buffer.GetSpan(1)[0] = value;
        _buffer.Advance(1);
    }

    /// <summary>Writes an <see cref="sbyte"/> (i8).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteI8(sbyte value) => WriteU8((byte)value);

    /// <summary>Writes a little-endian <see cref="ushort"/> (u16).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteU16(ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.GetSpan(2), value);
        _buffer.Advance(2);
    }

    /// <summary>Writes a little-endian <see cref="short"/> (i16).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteI16(short value)
    {
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.GetSpan(2), value);
        _buffer.Advance(2);
    }

    /// <summary>Writes a little-endian <see cref="uint"/> (u32).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteU32(uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
    }

    /// <summary>Writes a little-endian <see cref="int"/> (i32).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteI32(int value)
    {
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.GetSpan(4), value);
        _buffer.Advance(4);
    }

    /// <summary>Writes a little-endian <see cref="ulong"/> (u64).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteU64(ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.GetSpan(8), value);
        _buffer.Advance(8);
    }

    /// <summary>Writes a little-endian <see cref="long"/> (i64).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteI64(long value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.GetSpan(8), value);
        _buffer.Advance(8);
    }

    /// <summary>Writes a little-endian <see cref="UInt128"/> (u128).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteU128(UInt128 value)
    {
        BinaryPrimitives.WriteUInt128LittleEndian(_buffer.GetSpan(16), value);
        _buffer.Advance(16);
    }

    /// <summary>Writes a little-endian <see cref="Int128"/> (i128).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteI128(Int128 value)
    {
        BinaryPrimitives.WriteInt128LittleEndian(_buffer.GetSpan(16), value);
        _buffer.Advance(16);
    }

    /// <summary>Writes a Borsh bool as a single byte (<c>1</c> for true, <c>0</c> for false).</summary>
    /// <param name="value">The value to write.</param>
    public void WriteBool(bool value) => WriteU8(value ? (byte)1 : (byte)0);

    /// <summary>Writes a 32-byte <see cref="PublicKey"/>.</summary>
    /// <param name="value">The public key to write.</param>
    public void WritePublicKey(PublicKey value)
    {
        value.CopyTo(_buffer.GetSpan(PublicKey.Length));
        _buffer.Advance(PublicKey.Length);
    }

    /// <summary>Writes a length-prefixed UTF-8 string (a u32 byte-length, then the UTF-8 bytes).</summary>
    /// <param name="value">The string to write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <c>null</c>.</exception>
    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        WriteLength(bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>Writes raw bytes with no length prefix.</summary>
    /// <param name="value">The bytes to write.</param>
    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        value.CopyTo(_buffer.GetSpan(value.Length));
        _buffer.Advance(value.Length);
    }

    /// <summary>Writes a length-prefixed byte vector (a u32 length, then the bytes) - Borsh <c>Vec&lt;u8&gt;</c>.</summary>
    /// <param name="value">The bytes to write.</param>
    public void WriteByteVector(ReadOnlySpan<byte> value)
    {
        WriteLength(value.Length);
        WriteBytes(value);
    }

    /// <summary>Writes a u32 length prefix - as used by Borsh Vec and String. Write that many elements next.</summary>
    /// <param name="length">The length to write; must not be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public void WriteLength(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        WriteU32((uint)length);
    }

    /// <summary>Writes a Borsh Option tag: one byte, <c>1</c> for Some and <c>0</c> for None. Write the value next when <paramref name="hasValue"/> is true.</summary>
    /// <param name="hasValue"><c>true</c> to write a Some tag (a value follows); <c>false</c> for None.</param>
    public void WriteOption(bool hasValue) => WriteU8(hasValue ? (byte)1 : (byte)0);

    /// <summary>Returns the written bytes as a new array.</summary>
    /// <returns>The Borsh-encoded bytes.</returns>
    public byte[] ToArray() => _buffer.WrittenSpan.ToArray();
}
